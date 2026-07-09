using System;
using Godot;
using STS2Mobile.Launcher.Components;

namespace STS2Mobile.Launcher;

// Full-screen overlay shown during the launcher->game handoff (InitSettingsData +
// GameStartup), which can take many seconds while the game loads its data. Without
// it the launcher disappears and nothing is drawn until the game renders its first
// frame, so the app looks frozen. The animated label makes it clear work is ongoing.
public class LoadingScreen : Control
{
    private Label _label;
    private double _elapsed;
    private int _dots = -1;

    public void Initialize()
    {
        ZIndex = 100;
        SetAnchorsPreset(LayoutPreset.FullRect);

        try
        {
            var vpSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            Size = vpSize;
            var scale = Math.Max(vpSize.X, vpSize.Y) / 960f;

            AddChild(new ScreenBackground());

            var panel = new StyledPanel(scale, widthRatio: 0.5f);
            panel.UpdateSizeFromViewport(vpSize);
            AddChild(panel);

            _label = new StyledLabel("Loading", scale, fontSize: 20);
            panel.Content.AddChild(_label);

            PatchHelper.Log("[Loading] Handoff loading screen shown");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Loading] BuildUI failed: {ex.Message}");
        }
    }

    public override void _Process(double delta)
    {
        if (_label == null)
            return;

        // Animate the trailing dots ~3x/sec so the screen visibly moves whenever the
        // main thread yields — a static screen during a long load still reads as frozen.
        _elapsed += delta;
        int step = (int)(_elapsed * 3) % 4;
        if (step != _dots)
        {
            _dots = step;
            _label.Text = "Loading" + new string('.', step);
        }
    }
}
