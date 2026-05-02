using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;

namespace STS2Mobile.Patches;

// Works around a crash in MegaCrit.Sts2.Core.Localization.Fonts.FontControlUtils.
// ApplyLocaleFontSubstitution() throws a NullReferenceException when the platform
// locale is non-English (e.g. fr-FR), surfaces through LocString.GetFormattedText
// during the static .cctor of UI nodes (NTopBarFloorIcon / NPotionHolder), and
// brings the engine down before the main menu can load — the visible symptom is a
// hard black screen on launch.
//
// Letting the substitution call fail silently lets the engine continue with the
// default (already-loaded) font set, which renders the affected locales correctly.
public static class FontSubstitutionPatches
{
    private const string TargetTypeName =
        "MegaCrit.Sts2.Core.Localization.Fonts.FontControlUtils";
    private const string TargetMethodName = "ApplyLocaleFontSubstitution";

    private static int _suppressionCount;

    public static void Apply(Harmony harmony)
    {
        var sts2Asm = typeof(NGame).Assembly;
        var fontUtilsType = sts2Asm.GetType(TargetTypeName);
        if (fontUtilsType == null)
        {
            PatchHelper.Log(
                $"FontSubstitution: type {TargetTypeName} not present in sts2.dll; skipping"
            );
            return;
        }

        const BindingFlags methodFlags =
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Static
            | BindingFlags.Instance;

        var targetMethod = fontUtilsType.GetMethod(TargetMethodName, methodFlags);
        if (targetMethod == null)
        {
            PatchHelper.Log(
                $"FontSubstitution: {TargetTypeName}.{TargetMethodName} not found; skipping"
            );
            return;
        }

        var finalizerMethod = typeof(FontSubstitutionPatches).GetMethod(
            nameof(SwallowFinalizer),
            BindingFlags.Static | BindingFlags.NonPublic
        );

        try
        {
            harmony.Patch(targetMethod, finalizer: new HarmonyMethod(finalizerMethod));
            PatchHelper.Log(
                $"Patched {TargetTypeName}.{TargetMethodName} (finalizer; swallows locale errors)"
            );
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"FontSubstitution: failed to install finalizer: {ex.Message}");
        }
    }

    // Harmony finalizer convention: returning null marks the original exception
    // (passed via the special __exception parameter) as handled, so callers see
    // a normal return instead of an unhandled exception. Returning a non-null
    // Exception would replace it; returning the same instance would re-throw.
    private static Exception SwallowFinalizer(Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        // Only log the first swallow to avoid spamming on repeated calls.
        if (Interlocked.Increment(ref _suppressionCount) == 1)
        {
            PatchHelper.Log(
                $"FontSubstitution: suppressed {__exception.GetType().Name} "
                    + $"from {TargetMethodName} (\"{__exception.Message}\"); "
                    + "further occurrences will be silenced"
            );
        }

        return null;
    }
}
