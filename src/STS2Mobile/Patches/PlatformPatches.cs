using System;
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

        // Android can append Unicode locale extensions for regional preferences
        // (e.g. "de-DE-u-mu-celsius") which CultureInfo cannot parse here.
        PatchGetRawLanguage(harmony);
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

    private static void PatchGetRawLanguage(Harmony harmony)
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
                "GetRawLanguage",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (method == null)
            {
                PatchHelper.Log("Locale fix: GetRawLanguage not found, skipping");
                return;
            }

            harmony.Patch(
                method,
                postfix: new HarmonyMethod(
                    typeof(PlatformPatches).GetMethod(
                        nameof(GetRawLanguagePostfix),
                        BindingFlags.Public | BindingFlags.Static
                    )
                )
            );
            PatchHelper.Log("Patched NullPlatformUtilStrategy.GetRawLanguage (locale fix)");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"Locale fix failed: {ex.Message}");
        }
    }

    // Strip Unicode extension subtags before the game's language-code logic runs.
    public static void GetRawLanguagePostfix(ref string __result)
    {
        var raw = __result;
        var sanitized = StripUnicodeExtensions(raw?.Replace('_', '-') ?? "");
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            PatchHelper.Log($"Locale fix: raw='{raw}' sanitized empty; using 'en-US'");
            __result = "en-US";
            return;
        }

        if (sanitized != raw)
            PatchHelper.Log($"Locale fix: raw='{raw}' sanitized='{sanitized}'");

        __result = sanitized;
    }

    // Strips Unicode extension subtags while preserving the base locale.
    // Examples: "de-DE-u-mu-celsius" -> "de-DE", "en-US-#u-ms-metric" -> "en-US".
    private static string StripUnicodeExtensions(string locale)
    {
        var unicodeIdx = locale.IndexOf("-u-", StringComparison.OrdinalIgnoreCase);
        var legacyUnicodeIdx = locale.IndexOf("-#u-", StringComparison.OrdinalIgnoreCase);

        var idx = unicodeIdx;
        if (idx < 0 || (legacyUnicodeIdx >= 0 && legacyUnicodeIdx < idx))
            idx = legacyUnicodeIdx;

        return idx >= 0 ? locale.Substring(0, idx) : locale;
    }
}
