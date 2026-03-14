using System;
using System.Runtime.InteropServices;
using Godot;
using Godot.Bridge;
using Godot.NativeInterop;
using HarmonyLib;
using STS2Mobile.Launcher;
using STS2Mobile.Patches;

namespace STS2Mobile;

// Entry point for the mobile patcher. Bootstraps GodotSharp, applies all Harmony
// patches, and falls back to standalone launcher mode if game files aren't present.
public static class ModEntry
{
    private static Harmony _harmony;
    private static bool _applied = false;

    // Bootstraps GodotSharp by setting up DLL import resolver, native interop,
    // and managed callbacks. Called from gd_mono.cpp before Apply().
    [UnmanagedCallersOnly]
    public static int InitializeGodotSharp(
        IntPtr godotDllHandle,
        IntPtr outManagedCallbacks,
        IntPtr unmanagedCallbacks,
        int unmanagedCallbacksSize
    )
    {
        try
        {
            DllImportResolver dllImportResolver = new GodotDllImportResolver(
                godotDllHandle
            ).OnResolveDllImport;
            var coreApiAssembly = typeof(GodotObject).Assembly;
            NativeLibrary.SetDllImportResolver(coreApiAssembly, dllImportResolver);

            NativeFuncs.Initialize(unmanagedCallbacks, unmanagedCallbacksSize);
            ManagedCallbacks.Create(outManagedCallbacks);

            Console.Error.WriteLine("[STS2Mobile] GodotSharp bootstrapped successfully");
            return 1;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[STS2Mobile] GodotSharp bootstrap failed: {e}");
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    public static void Apply()
    {
        if (_applied)
            return;
        _applied = true;

        PatchHelper.Log("Initializing STS2Mobile...");

        _harmony = new Harmony("com.sts2mobile");

        // Game patches require sts2.dll; if missing, fall through to standalone launcher.
        try
        {
            ModelDbInitPatch.Apply(_harmony);
            PlatformPatches.Apply(_harmony);
            SettingsPatches.Apply(_harmony);
            UiScalePatches.Apply(_harmony);
            MobileLayoutPatches.Apply(_harmony);
            EventLayoutPatches.Apply(_harmony);
            MerchantLayoutPatches.Apply(_harmony);
            AppLifecyclePatches.Apply(_harmony);
            TouchInputPatches.Apply(_harmony);
            CardRewardPatches.Apply(_harmony);
            EarlyAccessDisclaimerPatches.Apply(_harmony);
            CombatBackgroundPatches.Apply(_harmony);
            LanMultiplayerPatcher.Apply(_harmony);
            ModLoaderPatches.Apply(_harmony);
            LauncherPatches.Apply(_harmony);
            SaveDiagnosticPatches.Apply(_harmony);

            PatchHelper.Log("All game patches applied.");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"Game patches skipped (files not present): {ex.Message}");
            ScheduleStandaloneLauncher();
        }
    }

    private static void ScheduleStandaloneLauncher()
    {
        PatchHelper.Log("Scheduling standalone launcher...");
        Callable.From(CreateStandaloneLauncher).CallDeferred();
    }

    private static void CreateStandaloneLauncher()
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            Callable.From(CreateStandaloneLauncher).CallDeferred();
            return;
        }

        var launcher = new LauncherUI();
        tree.Root.AddChild(launcher);
        launcher.Initialize();
        PatchHelper.Log("Standalone launcher displayed");
    }
}
