using Godot;

namespace STS2Mobile.Launcher.Components;

public class StyledLineEdit : LineEdit
{
    public StyledLineEdit(string placeholder, float scale, bool secret = false)
    {
        PlaceholderText = placeholder;
        Secret = secret;
        CustomMinimumSize = new Vector2(0, (int)(38 * scale));
        AddThemeFontSizeOverride("font_size", (int)(14 * scale));
        ContextMenuEnabled = true;
        ShortcutKeysEnabled = true;
        SelectAllOnFocus = true;
    }
}
