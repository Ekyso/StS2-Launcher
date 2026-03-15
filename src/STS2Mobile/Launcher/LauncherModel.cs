using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using STS2Mobile.Patches;
using STS2Mobile.Steam;

namespace STS2Mobile.Launcher;

// Orchestrates the launcher flow: credential loading, authentication, ownership
// verification, game file downloads, and update checks. Delegates persistence to
// SteamCredentialStore and ownership to OwnershipVerifier. Events fire from
// background threads; the controller marshals them to the main thread.
public class LauncherModel : IDisposable
{
    private readonly string _dataDir;
    private readonly SteamCredentialStore _credentialStore;

    private SteamConnection _connection;
    private SteamAuth _auth;
    private DepotDownloader _downloader;
    private CancellationTokenSource _downloadCts;
    private TaskCompletionSource<bool> _launchTcs;
    private TaskCompletionSource<string> _codeTcs;
    private SessionState _state = SessionState.Disconnected;
    private string _failReason;

    public volatile bool OfflineMode;
    public volatile bool ConnectionResolved;
    public volatile bool AwaitingCode;

    // True when launched from GameStartupWrapper (game files present). False in
    // standalone launcher mode where a restart is needed after downloading files.
    // Setting this to true eagerly creates the launch TCS so it exists before the
    // UI is shown (preventing a race between PLAY button and WaitForLaunch).
    private bool _inGameMode;
    public bool InGameMode
    {
        get => _inGameMode;
        set
        {
            _inGameMode = value;
            if (value && _launchTcs == null)
                _launchTcs = new TaskCompletionSource<bool>();
        }
    }
    public string AccountName => _credentialStore.AccountName;
    public string SavedAccountName => _credentialStore.AccountName;
    public string SavedRefreshToken => _credentialStore.RefreshToken;
    public string FailReason => _failReason;
    public SessionState SessionState => _state;

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
        _credentialStore = new SteamCredentialStore(dataDir);
    }

    public Task WaitForLaunch()
    {
        _launchTcs ??= new TaskCompletionSource<bool>();
        return _launchTcs.Task;
    }

    // Loads saved credentials and determines the launcher path. Sets
    // LauncherPatches statics so cloud push/pull works on all code paths.
    public FastPathResult StartSession()
    {
        OfflineMode = false;
        ConnectionResolved = false;
        _credentialStore.Load();

        if (_credentialStore.HasCredentials)
        {
            LauncherPatches.SavedAccountName = _credentialStore.AccountName;
            LauncherPatches.SavedRefreshToken = _credentialStore.RefreshToken;
        }

        var verifier = CreateOwnershipVerifier();
        var hasMarker = verifier?.HasMarker() ?? false;
        PatchHelper.Log(
            $"[Launcher] Fast path: creds={_credentialStore.HasCredentials}, marker={hasMarker}"
        );

        if (_credentialStore.HasCredentials && hasMarker)
            return FastPathResult.ReadyToLaunch;

        if (_credentialStore.HasCredentials)
            return FastPathResult.AutoConnect;

        return FastPathResult.ShowLogin;
    }

    // Connects on-demand and verifies ownership. Used when we have saved
    // credentials but no ownership marker.
    public async void Connect()
    {
        SetState(SessionState.Connecting);

        try
        {
            _connection = new SteamConnection(
                _credentialStore.AccountName,
                _credentialStore.RefreshToken
            );
            await VerifyOwnershipAsync();
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Launcher] Connection failed: {ex.Message}");
            SetState(
                SessionState.Failed,
                "Could not connect to Steam. Check your internet connection."
            );
        }
    }

    // Performs interactive login via SteamAuth, saves credentials on success,
    // then verifies ownership.
    public async Task LoginAsync(string username, string password)
    {
        SetState(SessionState.Authenticating);

        try
        {
            _auth = new SteamAuth();
            _auth.LogMessage += msg => LogReceived?.Invoke(msg);
            _auth.CodeProvider = async (wasIncorrect) =>
            {
                AwaitingCode = true;
                CodeNeeded?.Invoke(wasIncorrect);
                _codeTcs = new TaskCompletionSource<string>();
                var code = await _codeTcs.Task;

                if (_auth.NeedsReconnectForAuth)
                    await _auth.ReconnectForAuthAsync();

                AwaitingCode = false;
                return code;
            };

            _auth.Connect();
            var result = await _auth.LoginWithCredentialsAsync(
                username,
                password,
                _credentialStore.GuardData
            );

            _credentialStore.Save(result.AccountName, result.RefreshToken, result.GuardData);
            LauncherPatches.SavedAccountName = result.AccountName;
            LauncherPatches.SavedRefreshToken = result.RefreshToken;

            _auth.Dispose();
            _auth = null;

            _connection = new SteamConnection(result.AccountName, result.RefreshToken);
            await VerifyOwnershipAsync();
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Launcher] Login failed: {ex.Message}");
            SetState(SessionState.Failed, ex.Message);
            _auth?.Dispose();
            _auth = null;
        }
    }

    public void SubmitCode(string code) => _codeTcs?.TrySetResult(code);

    // Creates or reuses a SteamConnection for depot operations.
    public async Task EnsureConnectedAsync()
    {
        if (_state == SessionState.LoggedIn && _connection != null)
            return;

        if (!_credentialStore.HasCredentials)
        {
            SetState(SessionState.Failed, "No saved credentials");
            return;
        }

        _connection ??= new SteamConnection(
            _credentialStore.AccountName,
            _credentialStore.RefreshToken
        );

        SetState(SessionState.Connecting);
        try
        {
            await _connection.Apps.PICSGetAccessTokens(2868840, null);
            ConnectionResolved = true;
            OfflineMode = false;
            SetState(SessionState.LoggedIn);
        }
        catch (Exception ex)
        {
            SetState(SessionState.Failed, $"Connection failed: {ex.Message}");
        }
    }

    public async Task StartDownloadAsync()
    {
        await EnsureConnectedAsync();
        if (_state != SessionState.LoggedIn || _connection == null)
        {
            DownloadFailed?.Invoke(null);
            return;
        }

        _downloader?.Dispose();
        _downloader = new DepotDownloader(_connection, _dataDir);
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
            PatchHelper.Log($"[Launcher] Download error: {ex}");
        }
    }

    public async Task CheckForUpdatesAsync()
    {
        try
        {
            await EnsureConnectedAsync();
            if (_state != SessionState.LoggedIn || _connection == null)
            {
                UpdateCheckFailed?.Invoke("Not connected");
                return;
            }

            var downloader = new DepotDownloader(_connection, _dataDir);
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
        _connection?.Dispose();
        _connection = null;
        _auth?.Dispose();
        _auth = null;
        return StartSession();
    }

    public void Launch()
    {
        if (_credentialStore.HasCredentials)
        {
            LauncherPatches.SavedAccountName = _credentialStore.AccountName;
            LauncherPatches.SavedRefreshToken = _credentialStore.RefreshToken;
        }

        if (_launchTcs != null)
            _launchTcs.TrySetResult(true);
        else
        {
            PatchHelper.Log("[Launcher] Restarting app to load game files");
            GetGodotApp()?.Call("restartApp");
        }
    }

    public bool HasOwnershipMarker() => CreateOwnershipVerifier()?.HasMarker() ?? false;

    public void Dispose()
    {
        _downloadCts?.Cancel();
        _downloader?.Dispose();
        _auth?.Dispose();
        if (_launchTcs == null)
            _connection?.Dispose();
    }

    private async Task VerifyOwnershipAsync()
    {
        SetState(SessionState.VerifyingOwnership);

        var verifier = CreateOwnershipVerifier();
        bool owns = await verifier.VerifyAsync(_connection);

        if (owns)
        {
            PatchHelper.Log("[Launcher] Ownership verified");
            ConnectionResolved = true;
            SetState(SessionState.LoggedIn);
        }
        else
        {
            PatchHelper.Log("[Launcher] Ownership denied");
            SetState(
                SessionState.Failed,
                "You don't own Slay the Spire 2. Purchase on Steam to play."
            );
        }
    }

    private OwnershipVerifier CreateOwnershipVerifier()
    {
        var account = _credentialStore.AccountName;
        return account != null ? new OwnershipVerifier(_dataDir, account) : null;
    }

    private void SetState(SessionState state, string failReason = null)
    {
        _state = state;
        _failReason = failReason;
        SessionStateChanged?.Invoke(state);
    }

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

    private static string LocalBackupPrefPath =>
        Path.Combine(OS.GetDataDir(), "local_backup_enabled");

    public static bool LoadLocalBackupPref()
    {
        try
        {
            if (File.Exists(LocalBackupPrefPath))
                return File.ReadAllText(LocalBackupPrefPath).Trim() == "true";
        }
        catch { }
        return false;
    }

    public static void SaveLocalBackupPref(bool enabled)
    {
        try
        {
            File.WriteAllText(LocalBackupPrefPath, enabled ? "true" : "false");
        }
        catch { }
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
