using System;
using Godot;
using STS2Mobile.Launcher.Components;

namespace STS2Mobile.Launcher.Sections;

public class ActionSection : VBoxContainer
{
    public event Action LaunchPressed;
    public event Action RetryPressed;
    public event Action<bool> CloudSyncToggled;
    public event Action CheckForUpdatesPressed;

    private readonly Button _launchButton;
    private readonly Button _retryButton;
    private readonly StyledButton _cloudSyncToggle;
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

        _cloudSyncToggle = new StyledButton("Cloud Saves: OFF", scale, fontSize: 14, height: 44);
        _cloudSyncToggle.ToggleMode = true;
        _cloudSyncToggle.Visible = false;
        ApplyCloudSyncStyle(false);
        _cloudSyncToggle.Toggled += pressed =>
        {
            _cloudSyncToggle.Text = pressed ? "Cloud Saves: ON" : "Cloud Saves: OFF";
            ApplyCloudSyncStyle(pressed);
            CloudSyncToggled?.Invoke(pressed);
        };
        AddChild(_cloudSyncToggle);

        _updateButton = new StyledButton("CHECK FOR UPDATES", scale, fontSize: 16, height: 48);
        _updateButton.Visible = false;
        _updateButton.Pressed += () => CheckForUpdatesPressed?.Invoke();
        AddChild(_updateButton);

        _launchButton = new StyledButton("LAUNCH", scale, fontSize: 16, height: 48);
        _launchButton.Visible = false;
        _launchButton.Pressed += () => LaunchPressed?.Invoke();
        AddChild(_launchButton);
    }

    public void SetCloudSyncChecked(bool value)
    {
        _cloudSyncToggle.ButtonPressed = value;
        _cloudSyncToggle.Text = value ? "Cloud Saves: ON" : "Cloud Saves: OFF";
        ApplyCloudSyncStyle(value);
    }

    private void ApplyCloudSyncStyle(bool on)
    {
        var style = on ? _onStyle : _offStyle;
        _cloudSyncToggle.AddThemeStyleboxOverride("normal", style);
        _cloudSyncToggle.AddThemeStyleboxOverride("hover", style);
        _cloudSyncToggle.AddThemeStyleboxOverride("pressed", style);
        _cloudSyncToggle.AddThemeStyleboxOverride("disabled", style);
    }

    public void ShowLaunch(string text, bool showCloudSync, bool showUpdate)
    {
        _launchButton.Text = text;
        _launchButton.Visible = true;
        _cloudSyncToggle.Visible = showCloudSync;
        _updateButton.Visible = showUpdate;
        _updateButton.Disabled = false;
        _updateButton.Text = "CHECK FOR UPDATES";
        _retryButton.Visible = false;
    }

    public void ShowRetry()
    {
        _retryButton.Visible = true;
        _launchButton.Visible = false;
        _cloudSyncToggle.Visible = false;
        _updateButton.Visible = false;
    }

    public void HideAll()
    {
        _launchButton.Visible = false;
        _retryButton.Visible = false;
        _cloudSyncToggle.Visible = false;
        _updateButton.Visible = false;
    }

    public void SetUpdateButtonText(string text) => _updateButton.Text = text;

    public void SetUpdateButtonDisabled(bool disabled) => _updateButton.Disabled = disabled;
}
