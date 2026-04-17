using System;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;

namespace STS2Mobile.Patches;

// Post-game-update workaround: the game's
// MegaCrit.Sts2.Core.Localization.Fonts.FontControlUtils.ApplyLocaleFontSubstitution
// started throwing NullReferenceException on mobile after a recent StS2
// update. The exception propagates out of every MegaLabel/NMegaTextEdit
// _Ready(), which leaves the entire UI uninitialized and produces the
// "post-update blackscreen" symptom.
//
// We install a Harmony finalizer that swallows the exception. Labels
// miss their locale-specific font override but fall back to the theme
// default, so the menu renders with readable (if not pixel-perfect)
// text and the game becomes playable again.
public static class FontSubstitutionPatches
{
    private static bool _loggedFirstSwallow;

    public static void Apply(Harmony harmony)
    {
        var sts2Asm = typeof(NGame).Assembly;
        var fontUtilsType = sts2Asm.GetType(
            "MegaCrit.Sts2.Core.Localization.Fonts.FontControlUtils"
        );
        if (fontUtilsType == null)
        {
            PatchHelper.Log("FontSubstitutionPatches: FontControlUtils type not found; skipping");
            return;
        }

        var target = fontUtilsType.GetMethod(
            "ApplyLocaleFontSubstitution",
            BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Static
                | BindingFlags.Instance
        );
        if (target == null)
        {
            PatchHelper.Log(
                "FontSubstitutionPatches: ApplyLocaleFontSubstitution method not found; skipping"
            );
            return;
        }

        try
        {
            var finalizer = typeof(FontSubstitutionPatches).GetMethod(
                nameof(ApplyLocaleFontSubstitutionFinalizer),
                BindingFlags.NonPublic | BindingFlags.Static
            );
            harmony.Patch(target, finalizer: new HarmonyMethod(finalizer));
            PatchHelper.Log("Patched FontControlUtils.ApplyLocaleFontSubstitution (finalizer)");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"FontSubstitutionPatches: install failed: {ex.Message}");
        }
    }

    // Harmony finalizer signature: returning null suppresses the original
    // method's exception; returning a non-null Exception replaces it.
    private static Exception ApplyLocaleFontSubstitutionFinalizer(Exception __exception)
    {
        if (__exception == null)
            return null;

        if (!_loggedFirstSwallow)
        {
            _loggedFirstSwallow = true;
            PatchHelper.Log(
                $"FontSubstitutionPatches: suppressed {__exception.GetType().Name} "
                    + $"from ApplyLocaleFontSubstitution (\"{__exception.Message}\"); "
                    + "further occurrences silenced"
            );
        }

        return null;
    }
}
