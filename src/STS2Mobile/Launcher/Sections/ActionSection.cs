using System;
using Godot;
using STS2Mobile.Launcher.Components;

namespace STS2Mobile.Launcher.Sections;

public class ActionSection : VBoxContainer
{
    public event Action LaunchPressed;
    public event Action RetryPressed;
    public event Action<bool> LocalBackupToggled;
    public event Action<bool> CloudSyncToggled;
    public event Action CloudPushPressed;
    public event Action CloudPullPressed;
    public event Action CheckForUpdatesPressed;

    private readonly Button _launchButton;
    private readonly Button _retryButton;
    private readonly StyledButton _localBackupToggle;
    private readonly StyledButton _cloudSyncToggle;
    private readonly Button _pushButton;
    private readonly Button _pullButton;
    private readonly Button _updateButton;
    private readonly StyleBoxFlat _offStyle;
    private readonly StyleBoxFlat _onStyle;

    public ActionSection(float scale)
    {
        _retryButton = new StyledButton("RETRY", scale);
        _retryButton.Visible = false;
        _retryButton.Pressed += () => RetryPressed?.Invoke();
        AddChild(_retryButton);

        var r = (int)(4 * scale);
        var bw = System.Math.Max(1, (int)(2 * scale));
        _offStyle = StyledButton.MakeOutline(new Color(0.7f, 0.25f, 0.25f), r, bw);
        _onStyle = StyledButton.MakeOutline(new Color(0.25f, 0.65f, 0.3f), r, bw);

        _localBackupToggle = new StyledButton("Local Backup: OFF", scale, fontSize: 14, height: 44);
        _localBackupToggle.ToggleMode = true;
        _localBackupToggle.Visible = false;
        ApplyToggleStyle(_localBackupToggle, false);
        _localBackupToggle.Toggled += pressed =>
        {
            _localBackupToggle.Text = pressed ? "Local Backup: ON" : "Local Backup: OFF";
            ApplyToggleStyle(_localBackupToggle, pressed);
            LocalBackupToggled?.Invoke(pressed);
        };
        AddChild(_localBackupToggle);

        _cloudSyncToggle = new StyledButton("Auto Sync: OFF", scale, fontSize: 14, height: 44);
        _cloudSyncToggle.ToggleMode = true;
        _cloudSyncToggle.Visible = false;
        ApplyToggleStyle(_cloudSyncToggle, false);
        _cloudSyncToggle.Toggled += pressed =>
        {
            _cloudSyncToggle.Text = pressed ? "Auto Sync: ON" : "Auto Sync: OFF";
            ApplyToggleStyle(_cloudSyncToggle, pressed);
            CloudSyncToggled?.Invoke(pressed);
        };
        AddChild(_cloudSyncToggle);

        var pushPullRow = new HBoxContainer();
        pushPullRow.Visible = false;
        pushPullRow.AddThemeConstantOverride("separation", (int)(6 * scale));

        _pushButton = new StyledButton("Push to Cloud", scale, fontSize: 14, height: 44);
        _pushButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _pushButton.Pressed += () => CloudPushPressed?.Invoke();
        pushPullRow.AddChild(_pushButton);

        _pullButton = new StyledButton("Pull from Cloud", scale, fontSize: 14, height: 44);
        _pullButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _pullButton.Pressed += () => CloudPullPressed?.Invoke();
        pushPullRow.AddChild(_pullButton);

        AddChild(pushPullRow);

        _updateButton = new StyledButton("CHECK FOR UPDATES", scale, fontSize: 16, height: 48);
        _updateButton.Visible = false;
        _updateButton.Pressed += () => CheckForUpdatesPressed?.Invoke();
        AddChild(_updateButton);

        _launchButton = new StyledButton("LAUNCH", scale, fontSize: 16, height: 48);
        _launchButton.Visible = false;
        _launchButton.Pressed += () => LaunchPressed?.Invoke();
        AddChild(_launchButton);
    }

    public void SetLocalBackupChecked(bool value)
    {
        _localBackupToggle.ButtonPressed = value;
        _localBackupToggle.Text = value ? "Local Backup: ON" : "Local Backup: OFF";
        ApplyToggleStyle(_localBackupToggle, value);
    }

    public void SetCloudSyncChecked(bool value)
    {
        _cloudSyncToggle.ButtonPressed = value;
        _cloudSyncToggle.Text = value ? "Auto Sync: ON" : "Auto Sync: OFF";
        ApplyToggleStyle(_cloudSyncToggle, value);
    }

    private void ApplyToggleStyle(Button button, bool on)
    {
        var style = on ? _onStyle : _offStyle;
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", style);
        button.AddThemeStyleboxOverride("disabled", style);
    }

    private HBoxContainer PushPullRow => (HBoxContainer)_pushButton.GetParent();

    public void ShowLaunch(string text, bool showCloudSync, bool showUpdate)
    {
        _launchButton.Text = text;
        _launchButton.Visible = true;
        _localBackupToggle.Visible = showCloudSync;
        _cloudSyncToggle.Visible = showCloudSync;
        PushPullRow.Visible = showCloudSync;
        _updateButton.Visible = showUpdate;
        _updateButton.Disabled = false;
        _updateButton.Text = "CHECK FOR UPDATES";
        _retryButton.Visible = false;
    }

    public void ShowRetry()
    {
        _retryButton.Visible = true;
        _launchButton.Visible = false;
        _localBackupToggle.Visible = false;
        _cloudSyncToggle.Visible = false;
        PushPullRow.Visible = false;
        _updateButton.Visible = false;
    }

    public void HideAll()
    {
        _launchButton.Visible = false;
        _retryButton.Visible = false;
        _localBackupToggle.Visible = false;
        _cloudSyncToggle.Visible = false;
        PushPullRow.Visible = false;
        _updateButton.Visible = false;
    }

    public void SetPushPullDisabled(bool disabled)
    {
        _pushButton.Disabled = disabled;
        _pullButton.Disabled = disabled;
    }

    public void SetUpdateButtonText(string text) => _updateButton.Text = text;

    public void SetUpdateButtonDisabled(bool disabled) => _updateButton.Disabled = disabled;
}
