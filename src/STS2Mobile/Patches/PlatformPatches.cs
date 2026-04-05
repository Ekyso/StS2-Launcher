using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Mobile.Patches;

// Disables desktop-only platform features that are unavailable or unnecessary on mobile:
// Steam initialization, Sentry crash reporting, system info logging, and telemetry opt-in.
public static class PlatformPatches
{
    public static void Apply(Harmony harmony)
    {
        PatchHelper.Patch(
            harmony,
            typeof(NGame),
            "InitializePlatform",
            prefix: PatchHelper.Method(typeof(PlatformPatches), nameof(InitializePlatformPrefix))
        );

        PatchHelper.Patch(
            harmony,
            typeof(OsDebugInfo),
            "LogSystemInfo",
            prefix: PatchHelper.Method(typeof(PlatformPatches), nameof(SkipPrefix))
        );

        PatchHelper.PatchGetter(
            harmony,
            typeof(PrefsSave),
            "UploadData",
            prefix: PatchHelper.Method(typeof(PlatformPatches), nameof(ReturnFalsePrefix))
        );

        // NullPlatformUtilStrategy's constructor calls CreateDirectory(".") which
        // fails on Android because "." is not a valid absolute Godot path.
        PatchHelper.Patch(
            harmony,
            typeof(GodotFileIo),
            "CreateDirectory",
            prefix: PatchHelper.Method(typeof(PlatformPatches), nameof(CreateDirectoryPrefix))
        );

        // Skip Sentry crash reporting. Not useful for our mobile port and the
        // Sentry GDExtension is not bundled in the Android build.
        PatchHelper.Patch(
            harmony,
            typeof(SentryService),
            "Initialize",
            prefix: PatchHelper.Method(typeof(PlatformPatches), nameof(SkipPrefix))
        );

        // Android 16 uses extended locale identifiers with Unicode extensions
        // (e.g. "de-DE-u-mu-celsius") which .NET's CultureInfo cannot parse,
        // crashing the localization system and preventing the game from loading.
        PatchGetThreeLetterLanguageCode(harmony);
    }

    public static bool InitializePlatformPrefix(ref Task<bool> __result)
    {
        PatchHelper.Log("Skipping Steam initialization (mobile)");
        __result = Task.FromResult(true);
        return false;
    }

    public static bool SkipPrefix() => false;

    public static bool ReturnFalsePrefix(ref bool __result)
    {
        __result = false;
        return false;
    }

    // Skip paths that aren't valid Godot absolute paths (must contain "://").
    public static bool CreateDirectoryPrefix(GodotFileIo __instance, string directoryPath)
    {
        var fullPath = __instance.GetFullPath(directoryPath);
        if (!fullPath.Contains("://"))
            return false;
        return true;
    }

    private static void PatchGetThreeLetterLanguageCode(Harmony harmony)
    {
        try
        {
            var sts2Asm = typeof(NGame).Assembly;
            var nullStrategyType = sts2Asm.GetType(
                "MegaCrit.Sts2.Core.Platform.Null.NullPlatformUtilStrategy"
            );
            if (nullStrategyType == null)
            {
                PatchHelper.Log("Locale fix: NullPlatformUtilStrategy not found, skipping");
                return;
            }

            var method = nullStrategyType.GetMethod(
                "GetThreeLetterLanguageCode",
                BindingFlags.Public | BindingFlags.Instance
            );
            if (method == null)
            {
                PatchHelper.Log("Locale fix: GetThreeLetterLanguageCode not found, skipping");
                return;
            }

            harmony.Patch(
                method,
                prefix: new HarmonyMethod(
                    typeof(PlatformPatches).GetMethod(
                        nameof(GetThreeLetterLanguageCodePrefix),
                        BindingFlags.Public | BindingFlags.Static
                    )
                )
            );
            PatchHelper.Log("Patched NullPlatformUtilStrategy.GetThreeLetterLanguageCode (locale fix)");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"Locale fix failed: {ex.Message}");
        }
    }

    // Strip Unicode extension subtags (e.g. "-u-mu-celsius") from the locale
    // string before passing it to CultureInfo, which doesn't understand them.
    public static bool GetThreeLetterLanguageCodePrefix(ref string __result)
    {
        try
        {
            var locale = Godot.OS.GetLocale(); // e.g. "de_DE_u_mu_celsius" or "de_DE"
            var sanitized = StripUnicodeExtensions(locale.Replace('_', '-'));
            PatchHelper.Log($"Locale fix: raw='{locale}' sanitized='{sanitized}'");
            var culture = new CultureInfo(sanitized);
            __result = culture.ThreeLetterISOLanguageName;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"Locale fix: fallback to 'eng' due to: {ex.Message}");
            __result = "eng";
        }
        return false;
    }

    // Strips Unicode BCP 47 extension subtags: everything from "-u-" onward.
    // "de-DE-u-mu-celsius" → "de-DE", "en-US" → "en-US"
    private static string StripUnicodeExtensions(string locale)
    {
        var idx = locale.IndexOf("-u-", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? locale.Substring(0, idx) : locale;
    }
}
