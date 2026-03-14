using System;
using System.IO;
using System.Text.Json;
using Godot;

namespace STS2Mobile.Steam;

// Persists Steam account credentials encrypted with Android Keystore (AES-256-GCM).
// Reads and writes a single encrypted JSON file via the Java bridge to GodotApp.
public class SteamCredentialStore
{
    private readonly string _credentialsPath;
    private SteamCredentials _credentials;

    public string AccountName => _credentials?.AccountName;
    public string RefreshToken => _credentials?.RefreshToken;
    public string GuardData => _credentials?.GuardData;

    public bool HasCredentials =>
        _credentials?.RefreshToken != null && _credentials?.AccountName != null;

    public SteamCredentialStore(string dataDir)
    {
        _credentialsPath = Path.Combine(dataDir, "steam_credentials.enc");
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(_credentialsPath))
                return;

            var encrypted = File.ReadAllText(_credentialsPath);
            var godotApp = GetGodotApp();
            if (godotApp == null)
            {
                PatchHelper.Log("[Credentials] GodotApp not available for decryption");
                return;
            }

            var json = (string)godotApp.Call("decryptString", encrypted);
            if (json == null)
            {
                PatchHelper.Log("[Credentials] Decryption failed, deleting stale file");
                try
                {
                    File.Delete(_credentialsPath);
                }
                catch { }
                return;
            }

            _credentials = JsonSerializer.Deserialize<SteamCredentials>(json);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Credentials] Load failed: {ex.Message}");
            _credentials = null;
        }
    }

    public void Save(string accountName, string refreshToken, string guardData)
    {
        _credentials = new SteamCredentials
        {
            AccountName = accountName,
            RefreshToken = refreshToken,
            GuardData = guardData,
        };

        try
        {
            var dir = Path.GetDirectoryName(_credentialsPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var godotApp = GetGodotApp();
            if (godotApp == null)
            {
                PatchHelper.Log("[Credentials] GodotApp not available for encryption");
                return;
            }

            var json = JsonSerializer.Serialize(_credentials);
            var encrypted = (string)godotApp.Call("encryptString", json);
            if (encrypted == null)
            {
                PatchHelper.Log("[Credentials] Encryption returned null");
                return;
            }

            File.WriteAllText(_credentialsPath, encrypted);
            PatchHelper.Log("[Credentials] Saved (Android Keystore encrypted)");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Credentials] Save failed: {ex.Message}");
        }
    }

    public void Clear()
    {
        _credentials = null;
        try
        {
            File.Delete(_credentialsPath);
        }
        catch { }
        try
        {
            GetGodotApp()?.Call("deleteKeystoreKey");
        }
        catch { }
        PatchHelper.Log("[Credentials] Cleared");
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

    private class SteamCredentials
    {
        public string AccountName { get; set; }
        public string RefreshToken { get; set; }
        public string GuardData { get; set; }
    }
}
