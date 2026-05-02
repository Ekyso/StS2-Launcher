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
        try
        {
            using var dirAccess = DirAccess.Open(AppPaths.ExternalModsDir);
            if (dirAccess == null)
            {
                PatchHelper.Log(
                    $"[Mods] External mods directory not found: {AppPaths.ExternalModsDir} "
                        + $"(error: {DirAccess.GetOpenError()})"
                );
                return;
            }

            PatchHelper.Log($"[Mods] Scanning external mods: {AppPaths.ExternalModsDir}");

            // The current sts2.dll API splits mod loading in two:
            //   ReadModsInDirRecursive(path, source, newMods) — reads manifests
            //   TryLoadMod(mod)                                — loads a single mod
            // The previous LoadModsInDirRecursive(DirAccess, ModSource) atomic
            // call no longer exists, and load state is tracked directly via
            // Mod.state (ModLoadState enum) instead of a Mod.wasLoaded bool +
            // ModManager._loadedMods cache.
            var readMethod = typeof(ModManager).GetMethod("ReadModsInDirRecursive", AllStatic);
            var newMods = new List<Mod>();
            readMethod.Invoke(
                null,
                new object[] { AppPaths.ExternalModsDir, ModSource.ModsDirectory, newMods }
            );

            var tryLoadMethod = typeof(ModManager).GetMethod("TryLoadMod", AllStatic);
            foreach (var mod in newMods)
            {
                tryLoadMethod.Invoke(null, new object[] { mod });
            }

            // Ensure newly loaded mods are visible in ModManager.Mods so the
            // rest of the game can see them.
            var modsField = typeof(ModManager).GetField("_mods", AllStatic);
            var allMods = (List<Mod>)modsField.GetValue(null);
            foreach (var mod in newMods)
            {
                if (!allMods.Contains(mod))
                    allMods.Add(mod);
            }

            int loadedCount = newMods.Count(m => m.state == ModLoadState.Loaded);
            PatchHelper.Log(
                $"[Mods] External scan complete. {loadedCount}/{newMods.Count} loaded."
            );
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Mods] Failed to load external mods: {ex}");
        }
    }
}
