using Godot;

namespace STS2Mobile.Launcher.Components;

public class StyledProgressBar : ProgressBar
{
    public StyledProgressBar(float scale)
    {
        CustomMinimumSize = new Vector2(0, (int)(24 * scale));
    }
}
