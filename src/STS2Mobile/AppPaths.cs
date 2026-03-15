using System;
using System.IO;
using Godot;

namespace STS2Mobile;

// Shared path constants for external storage directories and permission helpers.
public static class AppPaths
{
    private const string ExternalRoot = "/storage/emulated/0/StS2Launcher";
    public const string ExternalModsDir = ExternalRoot + "/Mods";
    public const string ExternalSaveBackupsDir = ExternalRoot + "/Saves";

    // Returns true if the app has permission to write to shared external storage.
    public static bool HasStoragePermission()
    {
        try
        {
            var godotApp = GetGodotApp();
            if (godotApp == null)
                return false;
            return (bool)godotApp.Call("hasStoragePermission");
        }
        catch
        {
            return false;
        }
    }

    // Requests external storage permission. On Android 11+, opens the system
    // settings page. On older versions, shows the runtime permission dialog.
    public static void RequestStoragePermission()
    {
        try
        {
            var godotApp = GetGodotApp();
            godotApp?.Call("requestStoragePermission");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"Failed to request storage permission: {ex.Message}");
        }
    }

    // Creates the external Mods and Saves directories if storage permission is granted.
    public static void EnsureExternalDirectories()
    {
        if (!HasStoragePermission())
            return;

        try
        {
            Directory.CreateDirectory(ExternalModsDir);
        }
        catch { }
        try
        {
            Directory.CreateDirectory(ExternalSaveBackupsDir);
        }
        catch { }
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
}
