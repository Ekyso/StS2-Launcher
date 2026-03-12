using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using SteamKit2;
using SteamKit2.Authentication;

namespace STS2Mobile.Steam;

// Represents the current stage of the Steam connection and authentication flow.
public enum SessionState
{
    Disconnected,
    Connecting,
    WaitingForCredentials,
    Authenticating,
    VerifyingOwnership,
    LoggedIn,
    Failed,
}

// Manages the SteamKit2 connection lifecycle: connect, authenticate (password or
// saved refresh token), verify game ownership via PICS, and persist credentials
// encrypted with Android Keystore. Runs callbacks on a background thread.
public class SteamSession : IDisposable
{
    private SteamClient _client;
    private CallbackManager _callbackManager;
    private SteamUser _steamUser;
    private SteamApps _steamApps;
    private SteamContent _steamContent;
    private SteamUnifiedMessages _unifiedMessages;

    private Thread _callbackThread;
    private volatile bool _running;
    private bool _disposed;

    private SessionCredentials _savedCredentials;
    private string _credentialsPath;
    private string _ownershipMarkerPath;

    private const uint AppId = 2868840;
    private const int OwnershipMarkerValidDays = 30;
    private int _connectAttempts;

    // Defers reconnection until the user submits their 2FA code. The auth session
    // persists server-side, so only the transport needs to be restored.
    internal volatile bool NeedsReconnectForAuth;

    public SessionState State { get; private set; } = SessionState.Disconnected;
    public string AccountName { get; set; }
    public string SavedAccountName => _savedCredentials?.AccountName;
    public string FailReason { get; private set; }
    public SteamClient Client => _client;
    public SteamApps Apps => _steamApps;
    public SteamContent Content => _steamContent;
    public SteamUnifiedMessages UnifiedMessages => _unifiedMessages;

    public ReadOnlyCollection<SteamApps.LicenseListCallback.License> Licenses { get; private set; }

    // Set after ownership verification. Reused by DepotDownloader.
    public ulong AppAccessToken { get; private set; }

    public event Action<SessionState> StateChanged;
    public event Action<string> LogMessage;

    // Called when Steam Guard requires a 2FA or email code. The bool parameter
    // indicates whether the previous code was incorrect.
    public Func<bool, Task<string>> CodeProvider { get; set; }

    public bool HasSavedCredentials =>
        _savedCredentials?.RefreshToken != null && _savedCredentials?.AccountName != null;

    public SteamSession(string dataDir)
    {
        _credentialsPath = Path.Combine(dataDir, "steam_credentials.enc");
        _ownershipMarkerPath = Path.Combine(dataDir, "ownership_verified.enc");

        DebugLog.AddListener(new SteamDebugListener(this));
        var config = SteamConfiguration.Create(b => b.WithProtocolTypes(ProtocolTypes.WebSocket));
        _client = new SteamClient(config);
        _callbackManager = new CallbackManager(_client);
        _steamUser = _client.GetHandler<SteamUser>();
        _steamApps = _client.GetHandler<SteamApps>();
        _steamContent = _client.GetHandler<SteamContent>();
        _unifiedMessages = _client.GetHandler<SteamUnifiedMessages>();

        _callbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _callbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _callbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        _callbackManager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
        _callbackManager.Subscribe<SteamApps.LicenseListCallback>(OnLicenseList);

        LoadCredentials();
    }

    public void Connect()
    {
        if (State == SessionState.Connecting || State == SessionState.LoggedIn)
            return;

        _connectAttempts++;
        SetState(SessionState.Connecting);
        _running = true;

        if (_callbackThread == null || !_callbackThread.IsAlive)
        {
            _callbackThread = new Thread(CallbackLoop)
            {
                IsBackground = true,
                Name = "SteamCallbacks",
            };
            _callbackThread.Start();
        }

        _client.Connect();
        Log($"Connecting to Steam (attempt {_connectAttempts})...");
    }

    public async Task LoginWithCredentialsAsync(string username, string password)
    {
        if (
            State != SessionState.WaitingForCredentials
            && State != SessionState.Failed
            && State != SessionState.Disconnected
        )
            return;

        if (State == SessionState.Disconnected || State == SessionState.Failed)
        {
            Connect();
            for (int i = 0; i < 100 && State == SessionState.Connecting; i++)
                await Task.Delay(100);
            if (State == SessionState.Connecting || State == SessionState.Disconnected)
            {
                SetState(
                    SessionState.Failed,
                    "Could not connect to Steam. Check your internet connection."
                );
                return;
            }
        }

        SetState(SessionState.Authenticating);
        Log($"Authenticating as '{username}'...");

        try
        {
            var authSession = await _client.Authentication.BeginAuthSessionViaCredentialsAsync(
                new AuthSessionDetails
                {
                    Username = username,
                    Password = password,
                    IsPersistentSession = true,
                    GuardData = _savedCredentials?.GuardData,
                    Authenticator = new LauncherAuthenticator(this),
                }
            );

            // The WebSocket may drop during 2FA if the app is backgrounded.
            // OnDisconnected flags it for reconnection before the code is sent.
            var pollResponse = await authSession.PollingWaitForResultAsync();

            if (pollResponse.NewGuardData != null)
            {
                _savedCredentials ??= new SessionCredentials();
                _savedCredentials.GuardData = pollResponse.NewGuardData;
            }

            _savedCredentials ??= new SessionCredentials();
            _savedCredentials.AccountName = pollResponse.AccountName;
            _savedCredentials.RefreshToken = pollResponse.RefreshToken;
            SaveCredentials();

            _steamUser.LogOn(
                new SteamUser.LogOnDetails
                {
                    Username = pollResponse.AccountName,
                    AccessToken = pollResponse.RefreshToken,
                    ShouldRememberPassword = true,
                }
            );
        }
        catch (AuthenticationException ex)
        {
            SetState(SessionState.Failed, ex.Message);
        }
        catch (Exception ex)
        {
            SetState(SessionState.Failed, ex.Message);
        }
    }

    public void LoginWithSavedToken()
    {
        if (!HasSavedCredentials)
        {
            SetState(SessionState.WaitingForCredentials);
            return;
        }

        SetState(SessionState.Authenticating);
        Log($"Logging in as '{_savedCredentials.AccountName}' with saved token...");

        _steamUser.LogOn(
            new SteamUser.LogOnDetails
            {
                Username = _savedCredentials.AccountName,
                AccessToken = _savedCredentials.RefreshToken,
                ShouldRememberPassword = true,
            }
        );
    }

    public void ClearSavedCredentials()
    {
        _savedCredentials = null;
        try
        {
            File.Delete(_credentialsPath);
        }
        catch { }
        try
        {
            var godotApp = GetGodotApp();
            godotApp?.Call("deleteKeystoreKey");
        }
        catch { }
        Log("Saved credentials cleared");
    }

    public async Task<bool> VerifyOwnershipAsync()
    {
        SetState(SessionState.VerifyingOwnership);
        Log("Verifying game ownership...");

        try
        {
            var result = await _steamApps.PICSGetAccessTokens(AppId, null);
            bool owns = result.AppTokens.ContainsKey(AppId);

            if (owns)
            {
                result.AppTokens.TryGetValue(AppId, out var token);
                AppAccessToken = token;
                Log("Ownership verified");
                SaveOwnershipMarker();
                SetState(SessionState.LoggedIn);
            }
            else
            {
                Log("Ownership denied by Steam");
                SetState(
                    SessionState.Failed,
                    "You don't own Slay the Spire 2. Purchase on Steam to play."
                );
            }

            return owns;
        }
        catch (Exception ex)
        {
            Log($"Ownership check failed: {ex.Message}");
            if (HasValidOwnershipMarker())
            {
                Log("Using cached ownership marker (will re-verify next online launch)");
                SetState(SessionState.LoggedIn);
                return true;
            }
            SetState(SessionState.Failed, $"Cannot verify ownership: {ex.Message}");
            return false;
        }
    }

    public bool HasValidOwnershipMarker()
    {
        try
        {
            if (!File.Exists(_ownershipMarkerPath))
                return false;

            var godotApp = GetGodotApp();
            var json = (string)
                godotApp?.Call("decryptString", File.ReadAllText(_ownershipMarkerPath));
            if (json == null)
                return false;

            var marker = JsonSerializer.Deserialize<OwnershipMarker>(json);
            if (marker.Account != _savedCredentials?.AccountName)
                return false;

            var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - marker.VerifiedAt;
            return age >= 0 && age < OwnershipMarkerValidDays * 86400;
        }
        catch
        {
            return false;
        }
    }

    public void Disconnect()
    {
        _running = false;
        _steamUser?.LogOff();
        _client?.Disconnect();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _running = false;
        _client?.Disconnect();
        _callbackThread?.Join(2000);
    }

    private void OnConnected(SteamClient.ConnectedCallback cb)
    {
        Log("Connected to Steam");
        _connectAttempts = 0;

        // Transport restored for pending 2FA; skip normal login flow.
        if (NeedsReconnectForAuth)
        {
            NeedsReconnectForAuth = false;
            Log("Reconnected for auth code submission");
            return;
        }

        if (HasSavedCredentials)
            LoginWithSavedToken();
        else
            SetState(SessionState.WaitingForCredentials);
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback cb)
    {
        Log($"Disconnected from Steam (userInitiated={cb.UserInitiated})");

        // During 2FA, flag for reconnection instead of failing.
        if (!cb.UserInitiated && State == SessionState.Authenticating)
        {
            NeedsReconnectForAuth = true;
            Log("Connection lost during authentication — will reconnect on code submit");
            return;
        }

        NeedsReconnectForAuth = false;

        // No auto-reconnect. The refresh token is persistent; cloud sync
        // catches up next session.
        if (State == SessionState.LoggedIn)
            SetState(SessionState.Disconnected);
        else if (State == SessionState.Authenticating || State == SessionState.VerifyingOwnership)
            SetState(SessionState.Failed, "Connection lost during login. Tap LOG IN to retry.");
        else
            SetState(SessionState.Disconnected);
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback cb)
    {
        if (cb.Result == EResult.OK)
        {
            AccountName = _savedCredentials?.AccountName;
            Log($"Logged in as '{AccountName}'");
            _ = VerifyOwnershipAsync();
        }
        else if (
            cb.Result == EResult.InvalidPassword
            || cb.Result == EResult.InvalidSignature
            || cb.Result == EResult.AccessDenied
            || cb.Result == EResult.Expired
            || cb.Result == EResult.Revoked
        )
        {
            Log($"Saved token invalid ({cb.Result}), need fresh login");
            ClearSavedCredentials();
            SetState(SessionState.WaitingForCredentials);
        }
        else
        {
            SetState(SessionState.Failed, $"Login failed: {cb.Result} / {cb.ExtendedResult}");
        }
    }

    private void OnLicenseList(SteamApps.LicenseListCallback cb)
    {
        if (cb.Result == EResult.OK)
        {
            Licenses = cb.LicenseList;
            Log($"Got {cb.LicenseList.Count} licenses");
        }
    }

    private void OnLoggedOff(SteamUser.LoggedOffCallback cb)
    {
        Log($"Logged off: {cb.Result}");
        SetState(SessionState.Disconnected);
    }

    // Reconnects the WebSocket transport before submitting a 2FA code.
    internal async Task ReconnectForAuthAsync()
    {
        Log("Reconnecting for auth code submission...");
        Connect();
        for (int i = 0; i < 100; i++)
        {
            if (State != SessionState.Connecting)
                break;
            await Task.Delay(100);
        }
    }

    private void CallbackLoop()
    {
        while (_running)
        {
            _callbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
        }
    }

    private void SetState(SessionState state, string failReason = null)
    {
        State = state;
        FailReason = failReason;
        if (failReason != null)
            Log($"State: {state} - {failReason}");
        StateChanged?.Invoke(state);
    }

    private void Log(string msg)
    {
        PatchHelper.Log($"[Steam] {msg}");
        LogMessage?.Invoke(msg);
    }

    private void SaveOwnershipMarker()
    {
        try
        {
            var marker = JsonSerializer.Serialize(
                new OwnershipMarker
                {
                    Account = AccountName,
                    VerifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                }
            );
            var godotApp = GetGodotApp();
            var encrypted = (string)godotApp?.Call("encryptString", marker);
            if (encrypted != null)
                File.WriteAllText(_ownershipMarkerPath, encrypted);
        }
        catch (Exception ex)
        {
            Log($"Failed to save ownership marker: {ex.Message}");
        }
    }

    private void LoadCredentials()
    {
        try
        {
            if (!File.Exists(_credentialsPath))
                return;

            var encrypted = File.ReadAllText(_credentialsPath);
            var godotApp = GetGodotApp();
            if (godotApp == null)
            {
                Log("Cannot decrypt credentials: GodotApp not available");
                return;
            }

            var json = (string)godotApp.Call("decryptString", encrypted);
            if (json == null)
            {
                Log("Decryption failed, clearing stale credentials file");
                try
                {
                    File.Delete(_credentialsPath);
                }
                catch { }
                return;
            }

            _savedCredentials = JsonSerializer.Deserialize<SessionCredentials>(json);
        }
        catch (Exception ex)
        {
            Log($"Failed to load credentials: {ex.Message}");
            _savedCredentials = null;
        }
    }

    private void SaveCredentials()
    {
        try
        {
            var dir = Path.GetDirectoryName(_credentialsPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var godotApp = GetGodotApp();
            if (godotApp == null)
            {
                Log("Cannot encrypt credentials: GodotApp not available");
                return;
            }

            var json = JsonSerializer.Serialize(_savedCredentials);
            var encrypted = (string)godotApp.Call("encryptString", json);
            if (encrypted == null)
            {
                Log("Encryption failed");
                return;
            }

            File.WriteAllText(_credentialsPath, encrypted);
            Log("Credentials saved (Android Keystore encrypted)");
        }
        catch (Exception ex)
        {
            Log($"Failed to save credentials: {ex.Message}");
        }
    }

    private static GodotObject GetGodotApp()
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

    private class SessionCredentials
    {
        public string AccountName { get; set; }
        public string RefreshToken { get; set; }
        public string GuardData { get; set; }
    }

    private class OwnershipMarker
    {
        public string Account { get; set; }
        public long VerifiedAt { get; set; }
    }

    private class SteamDebugListener : IDebugListener
    {
        private readonly SteamSession _session;

        public SteamDebugListener(SteamSession session) => _session = session;

        public void WriteLine(string category, string msg) =>
            PatchHelper.Log($"[SK2/{category}] {msg}");
    }

    private class LauncherAuthenticator : IAuthenticator
    {
        private readonly SteamSession _session;

        public LauncherAuthenticator(SteamSession session)
        {
            _session = session;
        }

        public async Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
        {
            _session.Log(
                previousCodeWasIncorrect
                    ? "Previous 2FA code was incorrect, requesting new code"
                    : "Steam Guard 2FA code required"
            );

            if (_session.CodeProvider == null)
                throw new AuthenticationException("No code provider configured");

            return await _session.CodeProvider(previousCodeWasIncorrect);
        }

        public async Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
        {
            _session.Log(
                previousCodeWasIncorrect
                    ? "Previous email code was incorrect, requesting new code"
                    : $"Steam Guard email code sent to {email}"
            );

            if (_session.CodeProvider == null)
                throw new AuthenticationException("No code provider configured");

            return await _session.CodeProvider(previousCodeWasIncorrect);
        }

        public Task<bool> AcceptDeviceConfirmationAsync()
        {
            _session.Log("Waiting for Steam mobile app confirmation...");
            return Task.FromResult(true);
        }
    }
}
