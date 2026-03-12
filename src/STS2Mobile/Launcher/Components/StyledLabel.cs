using Godot;

namespace STS2Mobile.Launcher.Components;

public class StyledLabel : Label
{
    public StyledLabel(
        string text,
        float scale,
        int fontSize = 15,
        HorizontalAlignment align = HorizontalAlignment.Center
    )
    {
        Text = text;
        HorizontalAlignment = align;
        AddThemeFontSizeOverride("font_size", (int)(fontSize * scale));
    }
}
