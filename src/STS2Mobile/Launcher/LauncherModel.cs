using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using STS2Mobile.Steam;

namespace STS2Mobile.Launcher;

// Whether the launcher should show the login form or auto-connect with saved credentials.
public enum FastPathResult
{
    ShowLogin,
    AutoConnect,
}

// Manages Steam session lifecycle, game file downloads, and update checks.
// Events fire from SteamKit callback threads; the controller marshals them to the main thread.
public class LauncherModel : IDisposable
{
    private readonly string _dataDir;
    private SteamSession _session;
    private DepotDownloader _downloader;
    private CancellationTokenSource _downloadCts;
    private TaskCompletionSource<SteamSession> _launchTcs;
    private TaskCompletionSource<string> _codeTcs;

    public volatile bool OfflineMode;
    public volatile bool ConnectionResolved;
    public volatile bool AwaitingCode;

    public SteamSession Session => _session;
    public bool HasLaunchTcs => _launchTcs != null;
    public string AccountName => _session?.AccountName;
    public string FailReason => _session?.FailReason;
    public SessionState SessionState => _session?.State ?? SessionState.Disconnected;

    public event Action<SessionState> SessionStateChanged;
    public event Action<string> LogReceived;
    public event Action<bool> CodeNeeded;
    public event Action<DownloadProgress> DownloadProgressChanged;
    public event Action<string> DownloadLogReceived;
    public event Action DownloadCompleted;
    public event Action<string> DownloadFailed;
    public event Action DownloadCancelled;
    public event Action<bool> UpdateCheckCompleted;
    public event Action<string> UpdateCheckFailed;

    public LauncherModel(string dataDir)
    {
        _dataDir = dataDir;
    }

    public Task<SteamSession> WaitForLaunch()
    {
        _launchTcs = new TaskCompletionSource<SteamSession>();
        return _launchTcs.Task;
    }

    public FastPathResult StartSession()
    {
        OfflineMode = false;
        ConnectionResolved = false;
        _session = new SteamSession(_dataDir);
        _session.StateChanged += s => SessionStateChanged?.Invoke(s);
        _session.LogMessage += msg => LogReceived?.Invoke(msg);

        _session.CodeProvider = async (wasIncorrect) =>
        {
            AwaitingCode = true;
            CodeNeeded?.Invoke(wasIncorrect);
            _codeTcs = new TaskCompletionSource<string>();
            var code = await _codeTcs.Task;

            // Reconnect if WebSocket dropped while waiting (e.g. user backgrounded app to check authenticator).
            if (_session.NeedsReconnectForAuth)
                await _session.ReconnectForAuthAsync();

            AwaitingCode = false;
            return code;
        };

        var hasCreds = _session.HasSavedCredentials;
        PatchHelper.Log($"[Launcher] Fast path check: creds={hasCreds}");

        if (hasCreds)
            return FastPathResult.AutoConnect;

        return FastPathResult.ShowLogin;
    }

    public void Connect() => _session.Connect();

    public async Task LoginAsync(string username, string password)
    {
        try
        {
            await _session.LoginWithCredentialsAsync(username, password);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"Login error: {ex.Message}");
        }
    }

    public void SubmitCode(string code) => _codeTcs?.TrySetResult(code);

    public async Task EnsureConnectedAsync()
    {
        if (_session.State == SessionState.LoggedIn)
            return;

        if (
            _session.State != SessionState.Connecting
            && _session.State != SessionState.Authenticating
            && _session.State != SessionState.VerifyingOwnership
        )
        {
            _session.Connect();
        }

        for (int i = 0; i < 150; i++)
        {
            if (_session.State == SessionState.LoggedIn || _session.State == SessionState.Failed)
                break;
            await Task.Delay(100);
        }

        if (_session.State == SessionState.LoggedIn)
        {
            OfflineMode = false;
            ConnectionResolved = true;
        }
    }

    public async Task StartDownloadAsync()
    {
        await EnsureConnectedAsync();
        if (_session.State != SessionState.LoggedIn)
        {
            DownloadFailed?.Invoke(null);
            return;
        }

        _downloader?.Dispose();
        _downloader = new DepotDownloader(_session, _dataDir);
        _downloader.LogMessage += msg => DownloadLogReceived?.Invoke(msg);
        _downloader.ProgressChanged += p => DownloadProgressChanged?.Invoke(p);

        _downloadCts = new CancellationTokenSource();

        try
        {
            await Task.Run(() => _downloader.DownloadAsync(_downloadCts.Token));
            DownloadCompleted?.Invoke();
        }
        catch (OperationCanceledException)
        {
            DownloadCancelled?.Invoke();
        }
        catch (Exception ex)
        {
            DownloadFailed?.Invoke(ex.Message);
            PatchHelper.Log($"Download error: {ex}");
        }
    }

    public async Task CheckForUpdatesAsync()
    {
        try
        {
            await EnsureConnectedAsync();
            if (_session.State != SessionState.LoggedIn)
            {
                UpdateCheckFailed?.Invoke("Not connected");
                return;
            }

            var downloader = new DepotDownloader(_session, _dataDir);
            downloader.LogMessage += msg => DownloadLogReceived?.Invoke(msg);

            bool hasUpdate = await Task.Run(() => downloader.CheckForUpdatesAsync());
            downloader.Dispose();

            UpdateCheckCompleted?.Invoke(hasUpdate);
        }
        catch (Exception ex)
        {
            UpdateCheckFailed?.Invoke(ex.Message);
        }
    }

    public FastPathResult Retry()
    {
        _downloadCts?.Cancel();
        _downloader?.Dispose();
        _session?.Dispose();
        return StartSession();
    }

    public void Launch()
    {
        if (_launchTcs != null)
            _launchTcs.TrySetResult(OfflineMode ? null : _session);
        else
        {
            PatchHelper.Log("Restarting app to load game files...");
            GetGodotApp()?.Call("restartApp");
        }
    }

    public void Dispose()
    {
        _downloadCts?.Cancel();
        _downloader?.Dispose();
        if (_launchTcs == null)
            _session?.Dispose();
    }

    // Checks if the PCK file exists and has a valid Godot magic header.
    public static bool GameFilesReady()
    {
        var pckPath = Path.Combine(OS.GetDataDir(), "game", "SlayTheSpire2.pck");
        try
        {
            using var fs = File.OpenRead(pckPath);
            if (fs.Length < 4)
                return false;
            Span<byte> magic = stackalloc byte[4];
            fs.ReadExactly(magic);
            return magic[0] == 0x47 && magic[1] == 0x44 && magic[2] == 0x50 && magic[3] == 0x43;
        }
        catch
        {
            return false;
        }
    }

    public static string FormatSize(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        if (bytes >= 1024L * 1024)
            return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / 1024.0:F0} KB";
    }

    private static string CloudSyncPrefPath => Path.Combine(OS.GetDataDir(), "cloud_sync_enabled");

    public static bool LoadCloudSyncPref()
    {
        try
        {
            if (File.Exists(CloudSyncPrefPath))
                return File.ReadAllText(CloudSyncPrefPath).Trim() == "true";
        }
        catch { }
        return true;
    }

    public static void SaveCloudSyncPref(bool enabled)
    {
        try
        {
            File.WriteAllText(CloudSyncPrefPath, enabled ? "true" : "false");
        }
        catch { }
    }

    public static GodotObject GetGodotApp()
    {
        try
        {
            var jcw = Engine.GetSingleton("JavaClassWrapper");
            var wrapper = (GodotObject)jcw.Call("wrap", "com.game.sts2launcher.GodotApp");
            return (GodotObject)wrapper.Call("getInstance");
        }
        catch
        {
            return null;
        }
    }
}
