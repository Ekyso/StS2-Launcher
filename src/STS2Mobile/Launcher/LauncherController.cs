using System;
using System.Threading.Tasks;
using STS2Mobile.Patches;
using STS2Mobile.Steam;

namespace STS2Mobile.Launcher;

// Wires model events to view updates and handles the launcher UI state machine.
// All model callbacks are marshalled to the main thread before updating the view.
public class LauncherController
{
    private readonly LauncherModel _model;
    private readonly LauncherView _view;
    private readonly Action<Action> _runOnMainThread;
    private volatile bool _checkingForUpdates;

    public LauncherController(
        LauncherModel model,
        LauncherView view,
        Action<Action> runOnMainThread
    )
    {
        _model = model;
        _view = view;
        _runOnMainThread = runOnMainThread;
    }

    public void Start()
    {
        _model.SessionStateChanged += s => _runOnMainThread(() => UpdateUI(s));
        _model.LogReceived += msg => _runOnMainThread(() => _view.AppendLog(msg));
        _model.CodeNeeded += wasIncorrect =>
            _runOnMainThread(() =>
            {
                _view.Login.Visible = false;
                _view.Code.Show(wasIncorrect);
            });
        _model.DownloadProgressChanged += p =>
            _runOnMainThread(() =>
            {
                _view.Download.SetProgress(
                    p.Percentage,
                    $"{LauncherModel.FormatSize(p.DownloadedBytes)} / {LauncherModel.FormatSize(p.TotalBytes)} ({p.Percentage:F1}%)"
                );
                _view.AppendLog(p.CurrentFile);
            });
        _model.DownloadLogReceived += msg => _runOnMainThread(() => _view.AppendLog(msg));
        _model.DownloadCompleted += () =>
            _runOnMainThread(() =>
            {
                _view.SetStatus("Download complete! Restart to play.");
                _view.Download.Visible = false;
                if (LauncherModel.GameFilesReady())
                {
                    var text = _model.HasLaunchTcs ? "LAUNCH" : "RESTART APP";
                    _view.Actions.ShowLaunch(text, showCloudSync: false, showUpdate: false);
                }
                else
                    _view.Actions.ShowRetry();
            });
        _model.DownloadFailed += msg =>
            _runOnMainThread(() =>
            {
                if (msg == null)
                {
                    _view.Download.Reset();
                    return;
                }
                _view.SetStatus($"Download failed: {msg}");
                _view.Download.Reset("RETRY DOWNLOAD");
            });
        _model.DownloadCancelled += () =>
            _runOnMainThread(() =>
            {
                _view.SetStatus("Download cancelled");
                _view.Download.SetButtonDisabled(false);
            });
        _model.UpdateCheckCompleted += hasUpdate =>
            _runOnMainThread(() =>
            {
                if (hasUpdate)
                {
                    _view.Actions.HideAll();
                    _view.Download.Visible = true;
                    _view.Download.Reset("UPDATE GAME FILES");
                    _view.SetStatus("Update available!");
                }
                else
                {
                    _view.Actions.SetUpdateButtonText("UP TO DATE");
                }
            });
        _model.UpdateCheckFailed += msg =>
            _runOnMainThread(() =>
            {
                _view.Actions.SetUpdateButtonText("CHECK FAILED");
                _view.Actions.SetUpdateButtonDisabled(false);
                _view.AppendLog($"Update check failed: {msg}");
            });

        _view.Login.LoginRequested += OnLoginPressed;
        _view.Code.CodeSubmitted += OnCodeSubmitPressed;
        _view.Download.DownloadRequested += OnDownloadPressed;
        _view.Actions.LaunchPressed += OnLaunchPressed;
        _view.Actions.RetryPressed += OnRetryPressed;
        _view.Actions.CloudSyncToggled += OnCloudSyncToggled;
        _view.Actions.CheckForUpdatesPressed += OnCheckForUpdatesPressed;

        _view.Actions.SetCloudSyncChecked(LauncherModel.LoadCloudSyncPref());

        var result = _model.StartSession();
        HandleFastPath(result);
    }

    private void HandleFastPath(FastPathResult result)
    {
        switch (result)
        {
            case FastPathResult.AutoConnect:
                _model.Connect();
                StartConnectionTimeout();
                break;

            case FastPathResult.ShowLogin:
                _view.SetStatus("Enter your Steam credentials");
                _view.Login.Visible = true;
                _view.Login.SetDisabled(false);
                break;
        }
    }

    private async void StartConnectionTimeout()
    {
        await Task.Delay(10000);

        if (_model.ConnectionResolved)
            return;

        var state = _model.SessionState;
        if (
            state
            is SessionState.Connecting
                or SessionState.Authenticating
                or SessionState.VerifyingOwnership
        )
        {
            if (_model.Session.HasValidOwnershipMarker() && LauncherModel.GameFilesReady())
            {
                _runOnMainThread(() =>
                {
                    _view.SetStatus("No connection — launching offline");
                    _view.AppendLog("Connection timed out. Valid ownership marker found.");
                    _view.Actions.ShowLaunch(
                        "PLAY OFFLINE",
                        showCloudSync: false,
                        showUpdate: false
                    );
                });
                _model.OfflineMode = true;
            }
            else
            {
                _runOnMainThread(() =>
                {
                    _view.SetStatus("Connection failed. Internet required for first launch.");
                    _view.Actions.ShowRetry();
                });
            }
        }
    }

    // Updates visible sections and status text based on session state transitions.
    private void UpdateUI(SessionState state)
    {
        if (
            _model.AwaitingCode
            && state
                is SessionState.Connecting
                    or SessionState.WaitingForCredentials
                    or SessionState.Authenticating
        )
            return;

        if (_checkingForUpdates)
            return;

        _view.HideAllSections();

        switch (state)
        {
            case SessionState.Connecting:
                _view.SetStatus("Connecting to Steam...");
                break;

            case SessionState.WaitingForCredentials:
                _view.SetStatus("Enter your Steam credentials");
                _view.Login.Visible = true;
                _view.Login.SetDisabled(false);
                break;

            case SessionState.Authenticating:
                _view.SetStatus("Authenticating...");
                break;

            case SessionState.VerifyingOwnership:
                _view.SetStatus("Verifying game ownership...");
                break;

            case SessionState.LoggedIn:
                _model.ConnectionResolved = true;
                _view.SetStatus($"Logged in as {_model.AccountName}");
                if (LauncherModel.GameFilesReady())
                {
                    var text = _model.HasLaunchTcs ? "LAUNCH" : "RESTART APP";
                    _view.Actions.ShowLaunch(
                        text,
                        showCloudSync: !_model.OfflineMode,
                        showUpdate: true
                    );
                }
                else
                {
                    _view.Download.Visible = true;
                    _view.Download.SetButtonDisabled(false);
                }
                break;

            case SessionState.Failed:
                _model.ConnectionResolved = true;
                _view.SetStatus($"Error: {_model.FailReason}");
                _view.Login.Visible = true;
                _view.Login.SetDisabled(false);
                break;

            case SessionState.Disconnected:
                if (_model.ConnectionResolved && LauncherModel.GameFilesReady())
                {
                    _view.SetStatus("Disconnected from Steam");
                    _view.Actions.ShowLaunch(
                        "PLAY OFFLINE",
                        showCloudSync: false,
                        showUpdate: false
                    );
                    _model.OfflineMode = true;
                }
                else if (!_model.ConnectionResolved)
                {
                    _view.SetStatus("Enter your Steam credentials");
                    _view.Login.Visible = true;
                    _view.Login.SetDisabled(false);
                }
                else
                {
                    _view.SetStatus("Disconnected from Steam");
                    _view.Actions.ShowRetry();
                }
                break;
        }
    }

    private async void OnLoginPressed(string username, string password)
    {
        _view.Login.SetDisabled(true);
        _view.Login.ClearPassword();
        await _model.LoginAsync(username, password);
    }

    private void OnCodeSubmitPressed(string code)
    {
        _view.SetStatus("Verifying code...");
        _model.SubmitCode(code);
    }

    private async void OnDownloadPressed()
    {
        _view.Download.ShowProgress("Connecting to Steam...");
        await _model.StartDownloadAsync();
    }

    private async void OnCheckForUpdatesPressed()
    {
        _checkingForUpdates = true;
        _view.Actions.SetUpdateButtonDisabled(true);
        _view.Actions.SetUpdateButtonText("Checking...");
        await _model.CheckForUpdatesAsync();
        _checkingForUpdates = false;
    }

    private void OnCloudSyncToggled(bool pressed)
    {
        LauncherModel.SaveCloudSyncPref(pressed);
        LauncherPatches.CloudSyncEnabled = pressed;
    }

    private void OnRetryPressed()
    {
        var result = _model.Retry();
        HandleFastPath(result);
    }

    private void OnLaunchPressed() => _model.Launch();
}
