using Godot;

namespace STS2Mobile.Launcher.Components;

public class StyledButton : Button
{
    public StyledButton(string text, float scale, int fontSize = 14, int height = 42)
    {
        Text = text;
        CustomMinimumSize = new Vector2(0, (int)(height * scale));
        AddThemeFontSizeOverride("font_size", (int)(fontSize * scale));

        var r = (int)(4 * scale);
        AddThemeStyleboxOverride("normal", MakeFilled(new Color(0.25f, 0.25f, 0.3f), r));
        AddThemeStyleboxOverride("hover", MakeFilled(new Color(0.3f, 0.3f, 0.36f), r));
        AddThemeStyleboxOverride("pressed", MakeFilled(new Color(0.2f, 0.2f, 0.25f), r));
        AddThemeStyleboxOverride("disabled", MakeFilled(new Color(0.2f, 0.2f, 0.22f), r));
    }

    public static StyleBoxFlat MakeFilled(Color bg, int cornerRadius)
    {
        var style = new StyleBoxFlat();
        style.BgColor = bg;
        style.SetCornerRadiusAll(cornerRadius);
        return style;
    }

    public static StyleBoxFlat MakeOutline(Color borderColor, int cornerRadius, int borderWidth)
    {
        var style = new StyleBoxFlat();
        style.BgColor = Colors.Transparent;
        style.BorderColor = borderColor;
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(cornerRadius);
        return style;
    }
}
