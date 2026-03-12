using System;
using Godot;
using STS2Mobile.Launcher.Components;

namespace STS2Mobile.Launcher.Sections;

public class CodeSection : VBoxContainer
{
    public event Action<string> CodeSubmitted;

    private readonly Label _codeLabel;
    private readonly LineEdit _codeField;

    public CodeSection(float scale)
    {
        AddThemeConstantOverride("separation", (int)(6 * scale));
        Visible = false;

        _codeLabel = new StyledLabel("Enter Steam Guard code", scale, fontSize: 14);
        AddChild(_codeLabel);

        _codeField = new StyledLineEdit("Code", scale);
        _codeField.MaxLength = 10;
        _codeField.TextSubmitted += _ => OnSubmit();
        AddChild(_codeField);

        var submitButton = new StyledButton("SUBMIT", scale);
        submitButton.Pressed += OnSubmit;
        AddChild(submitButton);
    }

    public void Show(bool wasIncorrect)
    {
        Visible = true;
        _codeField.Text = "";
        _codeLabel.Text = wasIncorrect
            ? "Code was incorrect. Enter new code:"
            : "Enter Steam Guard code";
        _codeField.GrabFocus();
    }

    private void OnSubmit()
    {
        var code = _codeField.Text.Trim();
        if (string.IsNullOrEmpty(code))
            return;

        Visible = false;
        CodeSubmitted?.Invoke(code);
    }
}
