using System;
using Godot;

namespace STS2Mobile.Launcher.Components;

// Modal confirmation dialog built from styled launcher components.
// Renders as a dimmed overlay with a centered panel, message, and OK/Cancel buttons.
public class StyledDialog : ColorRect
{
    public event Action Confirmed;
    public event Action Cancelled;

    public StyledDialog(string message, float scale)
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        Color = new Color(0, 0, 0, 0.6f);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);

        var dialogBox = new PanelContainer();
        var boxStyle = new StyleBoxFlat();
        boxStyle.BgColor = new Color(0.15f, 0.15f, 0.18f);
        boxStyle.SetCornerRadiusAll((int)(8 * scale));
        boxStyle.SetContentMarginAll((int)(24 * scale));
        dialogBox.AddThemeStyleboxOverride("panel", boxStyle);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", (int)(16 * scale));
        dialogBox.AddChild(vbox);

        var label = new StyledLabel(message, scale, fontSize: 16);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2((int)(300 * scale), 0);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(label);

        var buttonRow = new HBoxContainer();
        buttonRow.AddThemeConstantOverride("separation", (int)(12 * scale));
        buttonRow.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(buttonRow);

        var cancelButton = new StyledButton("Cancel", scale, fontSize: 14, height: 44);
        cancelButton.CustomMinimumSize = new Vector2(
            (int)(120 * scale),
            cancelButton.CustomMinimumSize.Y
        );
        cancelButton.Pressed += () =>
        {
            QueueFree();
            Cancelled?.Invoke();
        };
        buttonRow.AddChild(cancelButton);

        var okButton = new StyledButton("OK", scale, fontSize: 14, height: 44);
        okButton.CustomMinimumSize = new Vector2((int)(120 * scale), okButton.CustomMinimumSize.Y);
        okButton.Pressed += () =>
        {
            QueueFree();
            Confirmed?.Invoke();
        };
        buttonRow.AddChild(okButton);

        center.AddChild(dialogBox);
        AddChild(center);
    }
}
