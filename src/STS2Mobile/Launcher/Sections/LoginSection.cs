using System;
using Godot;
using STS2Mobile.Launcher.Components;

namespace STS2Mobile.Launcher.Sections;

public class LoginSection : VBoxContainer
{
    public event Action<string, string> LoginRequested;

    public LineEdit UsernameField { get; }
    private readonly LineEdit _passwordField;
    private readonly Button _loginButton;

    public LoginSection(float scale)
    {
        AddThemeConstantOverride("separation", (int)(6 * scale));
        Visible = false;

        UsernameField = new StyledLineEdit("Steam Username", scale);
        UsernameField.TextSubmitted += _ => _passwordField.GrabFocus();
        AddChild(UsernameField);

        _passwordField = new StyledLineEdit("Password", scale, secret: true);
        _passwordField.TextSubmitted += _ => OnLoginPressed();
        AddChild(_passwordField);

        _loginButton = new StyledButton("LOGIN", scale);
        _loginButton.Pressed += OnLoginPressed;
        AddChild(_loginButton);
    }

    public void SetDisabled(bool disabled)
    {
        _loginButton.Disabled = disabled;
    }

    public void ClearPassword()
    {
        _passwordField.Text = "";
    }

    private void OnLoginPressed()
    {
        var username = UsernameField.Text.Trim();
        var password = _passwordField.Text;
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return;

        LoginRequested?.Invoke(username, password);
    }
}
