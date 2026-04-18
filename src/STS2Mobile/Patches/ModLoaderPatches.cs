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

            var initializedField = typeof(ModManager).GetField("_initialized", AllStatic);
            initializedField.SetValue(null, false);

            var loadMethod = typeof(ModManager).GetMethod("LoadModsInDirRecursive", AllStatic);
            loadMethod.Invoke(null, new object[] { dirAccess, ModSource.ModsDirectory });

            initializedField.SetValue(null, true);

            // Rebuild _loadedMods to include anything new. Resolve Mod.wasLoaded
            // and ModManager.LoadedMods via reflection: MegaCrit renamed both
            // post-update (to PascalCase or elsewhere) and we want the build
            // to survive further renames.
            const BindingFlags InstFlags =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            var modsField = typeof(ModManager).GetField("_mods", AllStatic);
            var loadedModsField = typeof(ModManager).GetField("_loadedMods", AllStatic);
            var allMods = (System.Collections.IList)modsField.GetValue(null);

            MemberInfo wasLoadedMember =
                (MemberInfo)typeof(Mod).GetField("wasLoaded", InstFlags)
                ?? typeof(Mod).GetField("WasLoaded", InstFlags)
                ?? (MemberInfo)typeof(Mod).GetProperty("wasLoaded", InstFlags)
                ?? typeof(Mod).GetProperty("WasLoaded", InstFlags);

            if (wasLoadedMember == null)
            {
                PatchHelper.Log(
                    "[Mods] Could not locate Mod.wasLoaded field/property; skipping filter."
                );
                return;
            }

            Func<object, bool> isLoaded = wasLoadedMember switch
            {
                FieldInfo fi => (m => (bool)fi.GetValue(m)),
                PropertyInfo pi => (m => (bool)pi.GetValue(m)),
                _ => (_ => false),
            };

            var filtered = new List<Mod>();
            foreach (var mod in allMods)
                if (isLoaded(mod))
                    filtered.Add((Mod)mod);
            loadedModsField.SetValue(null, filtered);

            PatchHelper.Log($"[Mods] External scan complete. Total loaded: {filtered.Count}");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Mods] Failed to load external mods: {ex}");
        }
    }
}
