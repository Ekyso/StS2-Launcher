namespace STS2Mobile.Launcher;

// Represents the current stage of the launcher's Steam connection and
// authentication flow. Drives the launcher UI state machine.
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

// Whether the launcher should show the login form, auto-connect, or go
// straight to the launch screen.
public enum FastPathResult
{
    ShowLogin,
    AutoConnect,
    ReadyToLaunch,
}
