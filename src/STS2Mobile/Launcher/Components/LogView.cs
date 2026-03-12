using Godot;

namespace STS2Mobile.Launcher.Components;

public class LogView : RichTextLabel
{
    public LogView(float scale)
    {
        CustomMinimumSize = new Vector2(0, (int)(120 * scale));
        ScrollFollowing = true;
        BbcodeEnabled = false;
        AddThemeFontSizeOverride("normal_font_size", (int)(11 * scale));
        AddThemeColorOverride("default_color", new Color(0.6f, 0.6f, 0.65f));

        var bg = new StyleBoxFlat();
        bg.BgColor = new Color(0.05f, 0.05f, 0.07f);
        bg.SetCornerRadiusAll((int)(4 * scale));
        bg.ContentMarginLeft = (int)(8 * scale);
        bg.ContentMarginRight = (int)(8 * scale);
        bg.ContentMarginTop = (int)(4 * scale);
        bg.ContentMarginBottom = (int)(4 * scale);
        AddThemeStyleboxOverride("normal", bg);
    }

    public void AppendLog(string msg) => AddText(msg + "\n");
}
