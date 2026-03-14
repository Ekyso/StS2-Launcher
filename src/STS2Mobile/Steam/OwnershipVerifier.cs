using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using SteamKit2;

namespace STS2Mobile.Steam;

// Verifies game ownership via Steam PICS and caches the result as an encrypted
// marker file. The marker persists indefinitely — ownership is checked once at
// initial login and never re-verified.
public class OwnershipVerifier
{
    private const uint AppId = 2868840;

    private readonly string _markerPath;
    private readonly string _accountName;

    public OwnershipVerifier(string dataDir, string accountName)
    {
        _markerPath = Path.Combine(dataDir, "ownership_verified.enc");
        _accountName = accountName;
    }

    public bool HasMarker()
    {
        try
        {
            if (!File.Exists(_markerPath))
                return false;

            var godotApp = GetGodotApp();
            var json = (string)godotApp?.Call("decryptString", File.ReadAllText(_markerPath));
            if (json == null)
                return false;

            var marker = JsonSerializer.Deserialize<Marker>(json);
            return marker.Account == _accountName;
        }
        catch
        {
            return false;
        }
    }

    // Queries Steam PICS for app ownership. On success, saves a permanent marker
    // and sets the connection's AppAccessToken for depot downloads. Returns true
    // if the account owns the game.
    public async Task<bool> VerifyAsync(SteamConnection connection)
    {
        var result = await connection.Apps.PICSGetAccessTokens(AppId, null);
        bool owns = result.AppTokens.ContainsKey(AppId);

        if (owns)
        {
            result.AppTokens.TryGetValue(AppId, out var token);
            connection.AppAccessToken = token;
            SaveMarker();
        }

        return owns;
    }

    private void SaveMarker()
    {
        try
        {
            var json = JsonSerializer.Serialize(
                new Marker
                {
                    Account = _accountName,
                    VerifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                }
            );
            var godotApp = GetGodotApp();
            var encrypted = (string)godotApp?.Call("encryptString", json);
            if (encrypted != null)
                File.WriteAllText(_markerPath, encrypted);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Ownership] Failed to save marker: {ex.Message}");
        }
    }

    private static GodotObject GetGodotApp()
    {
        try
        {
            var jcw = Engine.GetSingleton("JavaClassWrapper");
            var wrapper = (GodotObject)jcw.Call("wrap", "com.game.sts2launcher.GodotApp");
            return (GodotObject)wrapper.Call("getInstance");
        }
        catch
        {
            return null;
        }
    }

    private class Marker
    {
        public string Account { get; set; }
        public long VerifiedAt { get; set; }
    }
}
