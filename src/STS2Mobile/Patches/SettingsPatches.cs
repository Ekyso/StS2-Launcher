using System;
using System.IO;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace STS2Mobile.Patches;

// Applies mobile-friendly default settings on first launch and fixes the VSync
// toggle label bug where the Off and On display values are swapped.
public static class SettingsPatches
{
    private static bool _mobileDefaultsChecked;

    public static void Apply(Harmony harmony)
    {
        // Apply mobile defaults on first launch; user preferences are respected after that.
        PatchHelper.Patch(
            harmony,
            typeof(SaveManager),
            "InitSettingsData",
            postfix: PatchHelper.Method(typeof(SettingsPatches), nameof(InitSettingsDataPostfix))
        );

        // Fix swapped Off/On labels in the VSync settings UI (upstream bug).
        var vsyncPaginatorType = typeof(NGame).Assembly.GetType(
            "MegaCrit.Sts2.Core.Nodes.Screens.Settings.NVSyncPaginator"
        );
        if (vsyncPaginatorType != null)
        {
            PatchHelper.Patch(
                harmony,
                vsyncPaginatorType,
                "GetVSyncString",
                prefix: PatchHelper.Method(typeof(SettingsPatches), nameof(GetVSyncStringPrefix))
            );
        }
    }

    public static void InitSettingsDataPostfix()
    {
        if (_mobileDefaultsChecked)
            return;
        _mobileDefaultsChecked = true;

        var markerPath = Path.Combine(OS.GetUserDataDir(), ".mobile_defaults_applied");
        if (File.Exists(markerPath))
            return;

        try
        {
            var settings = SaveManager.Instance.SettingsSave;
            settings.VSync = VSyncType.On;
            settings.AspectRatioSetting = AspectRatioSetting.Auto;
            settings.Msaa = 0;
            SaveManager.Instance.SaveSettings();

            File.WriteAllText(markerPath, "1");
            PatchHelper.Log(
                "Applied mobile default settings (first launch): VSync=On, AspectRatio=Auto, Msaa=None"
            );
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"Failed to apply mobile defaults: {ex.Message}");
        }
    }

    public static bool GetVSyncStringPrefix(object vsyncType, ref string __result)
    {
        try
        {
            int val = (int)vsyncType;
            var sts2Asm = typeof(NGame).Assembly;
            var locStringType = sts2Asm.GetType("MegaCrit.Sts2.Core.Localization.LocString");
            var ctor = locStringType.GetConstructor(new[] { typeof(string), typeof(string) });
            var getTextMethod = locStringType.GetMethod("GetFormattedText", Type.EmptyTypes);

            string key = val switch
            {
                1 => "VSYNC_OFF",
                2 => "VSYNC_ON",
                3 => "VSYNC_ADAPTIVE",
                _ => "VSYNC_ADAPTIVE",
            };

            var locStr = ctor.Invoke(new object[] { "settings_ui", key });
            __result = (string)getTextMethod.Invoke(locStr, null);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"GetVSyncStringPrefix failed: {ex.Message}");
            __result = "On";
        }
        return false;
    }
}
