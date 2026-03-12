using Godot;

namespace STS2Mobile.Launcher.Components;

public class ScreenBackground : ColorRect
{
    public ScreenBackground()
    {
        Color = new Color(0.08f, 0.08f, 0.1f);
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
    }
}
