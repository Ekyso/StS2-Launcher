using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using STS2Mobile.Steam;

namespace STS2Mobile.Patches;

// Handles app backgrounding and foregrounding. Mutes audio, pauses the scene
// tree, flushes cloud writes on background. Opens the pause menu on resume.
public static class AppLifecyclePatches
{
    public static void Apply(Harmony harmony)
    {
        var bgHandlerType = typeof(MegaCrit.Sts2.Core.Nodes.NGame).Assembly.GetType(
            "MegaCrit.Sts2.Core.Nodes.NBackgroundModeHandler"
        );
        if (bgHandlerType != null)
        {
            PatchHelper.Patch(
                harmony,
                bgHandlerType,
                "EnterBackgroundMode",
                postfix: PatchHelper.Method(
                    typeof(AppLifecyclePatches),
                    nameof(EnterBackgroundPostfix)
                )
            );

            PatchHelper.Patch(
                harmony,
                bgHandlerType,
                "ExitBackgroundMode",
                prefix: PatchHelper.Method(
                    typeof(AppLifecyclePatches),
                    nameof(ExitBackgroundPrefix)
                )
            );
        }

        // Redirect NGame.Quit to restart the app instead of force-killing the process.
        PatchHelper.Patch(
            harmony,
            typeof(MegaCrit.Sts2.Core.Nodes.NGame),
            "Quit",
            prefix: PatchHelper.Method(typeof(AppLifecyclePatches), nameof(QuitPrefix))
        );
    }

    public static void EnterBackgroundPostfix(object __instance)
    {
        try
        {
            try
            {
                var nGameInstance = MegaCrit.Sts2.Core.Nodes.NGame.Instance;
                if (nGameInstance != null)
                {
                    var audioMgr = typeof(MegaCrit.Sts2.Core.Nodes.NGame)
                        .GetProperty("AudioManager", BindingFlags.Public | BindingFlags.Instance)
                        ?.GetValue(nGameInstance);
                    if (audioMgr != null)
                    {
                        audioMgr
                            .GetType()
                            .GetMethod("SetMasterVol", BindingFlags.Public | BindingFlags.Instance)
                            ?.Invoke(audioMgr, new object[] { 0f });
                    }
                }
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"Mute FMOD failed: {ex.Message}");
            }

            int masterBus = AudioServer.GetBusIndex("Master");
            AudioServer.SetBusMute(masterBus, true);

            var node = (Node)__instance;
            node.GetTree().Paused = true;

            // Flush pending cloud writes before the OS may kill the process
            try
            {
                SteamKit2CloudSaveStore.Instance?.Flush(5000);
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"Cloud flush on background failed: {ex.Message}");
            }

            PatchHelper.Log("App backgrounded: audio muted, SceneTree paused");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"EnterBackgroundPostfix failed: {ex.Message}");
        }
    }

    // Opens the pause menu on resume so the player can re-orient before gameplay continues.
    public static bool ExitBackgroundPrefix(object __instance)
    {
        try
        {
            var node = (Node)__instance;
            var tree = node.GetTree();

            if (!tree.Paused)
                return true;

            // Show pause menu while tree is still paused so it renders on the first visible frame
            try
            {
                var nGameInstance = MegaCrit.Sts2.Core.Nodes.NGame.Instance;
                if (nGameInstance != null)
                {
                    var currentRunNode = typeof(MegaCrit.Sts2.Core.Nodes.NGame)
                        .GetProperty("CurrentRunNode", BindingFlags.Public | BindingFlags.Instance)
                        ?.GetValue(nGameInstance);

                    if (currentRunNode != null)
                    {
                        var globalUi = currentRunNode
                            .GetType()
                            .GetProperty("GlobalUi", BindingFlags.Public | BindingFlags.Instance)
                            ?.GetValue(currentRunNode);

                        if (globalUi != null)
                        {
                            var submenuStack = globalUi
                                .GetType()
                                .GetProperty(
                                    "SubmenuStack",
                                    BindingFlags.Public | BindingFlags.Instance
                                )
                                ?.GetValue(globalUi);

                            if (submenuStack != null)
                            {
                                var sts2Asm = typeof(MegaCrit.Sts2.Core.Nodes.NGame).Assembly;
                                var capContainerType = sts2Asm.GetType(
                                    "MegaCrit.Sts2.Core.Nodes.Screens.Capstones.NCapstoneContainer"
                                );
                                var capInstance = capContainerType
                                    .GetProperty(
                                        "Instance",
                                        BindingFlags.Public | BindingFlags.Static
                                    )
                                    ?.GetValue(null);
                                var currentScreen = capContainerType
                                    ?.GetProperty(
                                        "CurrentCapstoneScreen",
                                        BindingFlags.Public | BindingFlags.Instance
                                    )
                                    ?.GetValue(capInstance);

                                if (currentScreen == null)
                                {
                                    var enumType = sts2Asm.GetType(
                                        "MegaCrit.Sts2.Core.Nodes.Screens.CapstoneSubmenuType"
                                    );
                                    var pauseMenuVal = Enum.ToObject(enumType, 4); // PauseMenu = 4
                                    var showScreen = submenuStack
                                        .GetType()
                                        .GetMethod(
                                            "ShowScreen",
                                            BindingFlags.Public | BindingFlags.Instance
                                        );
                                    showScreen?.Invoke(submenuStack, new object[] { pauseMenuVal });
                                    PatchHelper.Log("Opened pause menu on resume");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"Failed to open pause menu: {ex.Message}");
            }

            tree.Paused = false;

            // Restore FMOD and Godot audio to user's saved volume levels
            int masterBus = AudioServer.GetBusIndex("Master");
            AudioServer.SetBusMute(masterBus, false);
            try
            {
                var nGameInstance = MegaCrit.Sts2.Core.Nodes.NGame.Instance;
                if (nGameInstance != null)
                {
                    var audioMgr = typeof(MegaCrit.Sts2.Core.Nodes.NGame)
                        .GetProperty("AudioManager", BindingFlags.Public | BindingFlags.Instance)
                        ?.GetValue(nGameInstance);
                    var saveManager = MegaCrit.Sts2.Core.Saves.SaveManager.Instance;
                    if (audioMgr != null && saveManager != null)
                    {
                        var settings = saveManager.SettingsSave;
                        var masterVol = (float)
                            settings
                                .GetType()
                                .GetProperty(
                                    "VolumeMaster",
                                    BindingFlags.Public | BindingFlags.Instance
                                )
                                ?.GetValue(settings);
                        audioMgr
                            .GetType()
                            .GetMethod("SetMasterVol", BindingFlags.Public | BindingFlags.Instance)
                            ?.Invoke(audioMgr, new object[] { masterVol });
                    }
                }
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"Restore audio failed: {ex.Message}");
            }

            PatchHelper.Log("App resumed: SceneTree unpaused, audio restored");

            var isBackgroundedField = AccessTools.Field(__instance.GetType(), "_isBackgrounded");
            var savedFpsField = AccessTools.Field(__instance.GetType(), "_savedMaxFps");

            if ((bool)isBackgroundedField.GetValue(__instance))
            {
                isBackgroundedField.SetValue(__instance, false);
                Engine.MaxFps = (int)savedFpsField.GetValue(__instance);
            }

            return false;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"ExitBackgroundPrefix failed: {ex.Message}");
            return true;
        }
    }

    // Replaces the default quit (force-kill) with a clean app restart via GodotApp.
    // Saves are already written by the original Quit() callers before this runs.
    public static bool QuitPrefix(object __instance)
    {
        try
        {
            try { SteamKit2CloudSaveStore.Instance?.Flush(5000); } catch { }

            PatchHelper.Log("NGame.Quit intercepted, restarting app");
            var jcw = Engine.GetSingleton("JavaClassWrapper");
            var wrapper = (GodotObject)jcw.Call("wrap", "com.game.sts2launcher.GodotApp");
            var godotApp = (GodotObject)wrapper.Call("getInstance");
            godotApp.Call("restartApp");
            return false;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"QuitPrefix failed, falling back to default: {ex.Message}");
            return true;
        }
    }
}
