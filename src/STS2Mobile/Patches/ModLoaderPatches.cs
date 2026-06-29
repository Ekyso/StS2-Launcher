using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace STS2Mobile.Patches;

// Extends ModManager to scan an external mods directory on Android so users
// can sideload mods to /storage/emulated/0/StS2Launcher/Mods/ without root.
public static class ModLoaderPatches
{
    private static readonly BindingFlags AllStatic =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static void Apply(Harmony harmony)
    {
        PatchHelper.Patch(
            harmony,
            typeof(ModManager),
            "Initialize",
            postfix: PatchHelper.Method(typeof(ModLoaderPatches), nameof(InitializePostfix))
        );
    }

    // Runs after the original Initialize() to pick up mods from external storage.
    // Temporarily clears _initialized so TryLoadModFromPck accepts new entries.
    public static void InitializePostfix()
    {
        // The base-game update refactored ModManager: it removed the _initialized and
        // _loadedMods fields, renamed LoadModsInDirRecursive -> ReadModsInDirRecursive,
        // replaced Mod.wasLoaded with Mod.state, and ModManager.LoadedMods with
        // ModManager.GetLoadedMods(). The external-mods sideload logic here relied on the
        // old shape, so it is disabled until ported. Steam Workshop mods are unaffected
        // (the game's own ModManager.Initialize still handles those).
        PatchHelper.Log(
            "[Mods] External-mods sideloading disabled on this build "
                + "(pending port to the updated ModManager API)."
        );
    }
}
