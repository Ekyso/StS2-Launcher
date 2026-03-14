using System;
using Godot;
using STS2Mobile.Launcher.Components;
using STS2Mobile.Launcher.Sections;

namespace STS2Mobile.Launcher;

// Builds the launcher UI layout programmatically with a split panel:
// left side has login/download/action controls, right side has a console log.
public class LauncherView
{
    public LoginSection Login { get; }
    public CodeSection Code { get; }
    public DownloadSection Download { get; }
    public ActionSection Actions { get; }
    public LogView Log { get; }

    private readonly StyledLabel _statusLabel;
    private readonly Control _parent;
    private readonly StyledPanel _panel;
    private float _panelBaseY;

    public LauncherView(Control parent, float scale)
    {
        _parent = parent;
        _scale = scale;
        parent.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var vpSize = parent.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);

        var bg = new ScreenBackground();
        bg.GuiInput += DismissKeyboard;
        parent.AddChild(bg);

        _panel = new StyledPanel(scale, widthRatio: 0.9f);
        _panel.UpdateSizeFromViewport(vpSize);
        _panel.Panel.GuiInput += DismissKeyboard;
        parent.AddChild(_panel);
        _panelBaseY = _panel.Position.Y;

        var hbox = new HBoxContainer();
        hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        hbox.AddThemeConstantOverride("separation", (int)(16 * scale));
        _panel.Content.AddChild(hbox);

        var leftCenter = new CenterContainer();
        leftCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        leftCenter.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        leftCenter.SizeFlagsStretchRatio = 1f;
        hbox.AddChild(leftCenter);

        var left = new VBoxContainer();
        left.CustomMinimumSize = new Vector2((int)(200 * scale), 0);
        left.AddThemeConstantOverride("separation", (int)(10 * scale));
        leftCenter.AddChild(left);

        var title = new StyledLabel("StS2 Launcher", scale, fontSize: 26);
        left.AddChild(title);
        left.AddChild(new HSeparator());

        _statusLabel = new StyledLabel("Initializing...", scale);
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        left.AddChild(_statusLabel);

        Login = new LoginSection(scale);
        left.AddChild(Login);

        Code = new CodeSection(scale);
        left.AddChild(Code);

        Download = new DownloadSection(scale);
        left.AddChild(Download);

        Actions = new ActionSection(scale);
        left.AddChild(Actions);

        // FMOD attribution (required by FMOD EULA).
        var fmodContainer = new VBoxContainer();
        fmodContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        fmodContainer.Alignment = BoxContainer.AlignmentMode.End;
        left.AddChild(fmodContainer);

        var fmodLogo = LoadFmodLogo(scale);
        if (fmodLogo != null)
            fmodContainer.AddChild(fmodLogo);

        var fmodCredit = new StyledLabel(
            "Made using FMOD Studio by Firelight Technologies Pty Ltd.",
            scale,
            fontSize: 8
        );
        fmodCredit.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
        fmodContainer.AddChild(fmodCredit);

        var right = new VBoxContainer();
        right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        right.SizeFlagsStretchRatio = 4f;
        hbox.AddChild(right);

        var logTitle = new StyledLabel("Console", scale, fontSize: 14);
        logTitle.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
        right.AddChild(logTitle);

        Log = new LogView(scale);
        Log.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        Log.GuiInput += DismissKeyboard;
        right.AddChild(Log);
    }

    private readonly float _scale;

    public void SetStatus(string text) => _statusLabel.Text = text;

    public void AppendLog(string msg) => Log.AppendLog(msg);

    public void HideAllSections()
    {
        Login.Visible = false;
        Code.Visible = false;
        Download.Visible = false;
        Actions.HideAll();
    }

    public void UpdateKeyboardOffset()
    {
        var kbHeight = DisplayServer.VirtualKeyboardGetHeight();
        if (kbHeight > 0)
        {
            var windowSize = DisplayServer.WindowGetSize();
            var vpSize = _parent.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            var scale = vpSize.Y / windowSize.Y;
            var offset = kbHeight * scale * 0.5f;
            _panel.Position = new Vector2(_panel.Position.X, _panelBaseY - offset);
        }
        else
        {
            _panel.Position = new Vector2(_panel.Position.X, _panelBaseY);
        }
    }

    // Loads the FMOD logo extracted by GodotApp from internal storage.
    private static TextureRect LoadFmodLogo(float scale)
    {
        try
        {
            var logoPath = System.IO.Path.Combine(OS.GetDataDir(), "fmod_logo.png");
            if (!System.IO.File.Exists(logoPath))
            {
                PatchHelper.Log($"FMOD logo not found at {logoPath}");
                return null;
            }

            var bytes = System.IO.File.ReadAllBytes(logoPath);
            var image = new Image();
            image.LoadPngFromBuffer(bytes);

            var tex = ImageTexture.CreateFromImage(image);
            var rect = new TextureRect();
            rect.Texture = tex;
            rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            rect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            rect.CustomMinimumSize = new Vector2((int)(120 * scale), (int)(30 * scale));
            return rect;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"Failed to load FMOD logo: {ex.Message}");
            return null;
        }
    }

    public void ShowConfirmation(string message, Action onConfirmed)
    {
        var dialog = new StyledDialog(message, _scale);
        dialog.Confirmed += onConfirmed;
        _parent.AddChild(dialog);
    }

    private void DismissKeyboard(InputEvent ev)
    {
        if (
            ev is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true }
        )
            _parent.GetViewport()?.GuiReleaseFocus();
    }
}
