using System;
using Godot;
using STS2Mobile.Launcher.Components;

namespace STS2Mobile.Launcher.Sections;

public class DownloadSection : VBoxContainer
{
    public event Action DownloadRequested;

    private readonly Button _downloadButton;
    private readonly ProgressBar _progressBar;
    private readonly Label _progressLabel;

    public DownloadSection(float scale)
    {
        AddThemeConstantOverride("separation", (int)(6 * scale));
        Visible = false;

        _downloadButton = new StyledButton("DOWNLOAD GAME FILES", scale, height: 48);
        _downloadButton.Pressed += () => DownloadRequested?.Invoke();
        AddChild(_downloadButton);

        _progressBar = new StyledProgressBar(scale);
        _progressBar.Visible = false;
        AddChild(_progressBar);

        _progressLabel = new StyledLabel("", scale, fontSize: 12);
        _progressLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
        _progressLabel.Visible = false;
        AddChild(_progressLabel);
    }

    public void SetProgress(double pct, string text)
    {
        _progressBar.Visible = true;
        _progressBar.Value = pct;
        _progressLabel.Visible = true;
        _progressLabel.Text = text;
    }

    public void ShowProgress(string text)
    {
        _downloadButton.Disabled = true;
        _progressBar.Visible = true;
        _progressBar.Value = 0;
        _progressLabel.Visible = true;
        _progressLabel.Text = text;
    }

    public void HideProgress()
    {
        _progressBar.Visible = false;
        _progressLabel.Visible = false;
    }

    public void SetButtonDisabled(bool disabled) => _downloadButton.Disabled = disabled;

    public void SetButtonText(string text) => _downloadButton.Text = text;

    public void Reset(string buttonText = "DOWNLOAD GAME FILES")
    {
        _downloadButton.Disabled = false;
        _downloadButton.Text = buttonText;
        _progressBar.Visible = false;
        _progressBar.Value = 0;
        _progressLabel.Visible = false;
    }
}
