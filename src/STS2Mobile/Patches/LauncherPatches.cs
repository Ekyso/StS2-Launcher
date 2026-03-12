using System;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves;
using STS2Mobile.Launcher;
using STS2Mobile.Steam;

namespace STS2Mobile.Patches;

// Core patches for the mobile launcher flow. Intercepts GameStartupWrapper to show
// the Steam login UI before the game starts, injects cloud save support via SteamKit2,
// and replaces the cloud sync logic with timestamp-aware conflict resolution.
public static class LauncherPatches
{
    // Set after login. Null means offline mode (local-only saves).
    internal static SteamSession Session;
    internal static bool CloudSyncEnabled = true;

    public static void Apply(Harmony harmony)
    {
        // Gates ownership verification. Must succeed or the game launches unchecked.
        PatchHelper.PatchCritical(
            harmony,
            typeof(NGame),
            "GameStartupWrapper",
            prefix: PatchHelper.Method(typeof(LauncherPatches), nameof(GameStartupWrapperPrefix))
        );

        // Inject cloud saves into SaveManager. May fire before Session is set
        // (during atlas loading); RunLauncherThenGame resets _instance afterward.
        PatchHelper.Patch(
            harmony,
            typeof(SaveManager),
            "ConstructDefault",
            prefix: PatchHelper.Method(typeof(LauncherPatches), nameof(ConstructDefaultPrefix))
        );

        // Use timestamp comparison instead of strict equality for cloud sync.
        // Prevents overwriting newer local saves with older cloud data when
        // background uploads haven't completed yet.
        PatchHelper.PatchCritical(
            harmony,
            typeof(CloudSaveStore),
            "SyncCloudToLocal",
            prefix: PatchHelper.Method(typeof(LauncherPatches), nameof(SyncCloudToLocalPrefix))
        );
    }

    public static bool GameStartupWrapperPrefix(object __instance, ref Task __result)
    {
        __result = RunLauncherThenGame(__instance);
        return false;
    }

    // If a Steam session is available, creates a SaveManager backed by CloudSaveStore
    // so all writes go to both local storage and Steam cloud.
    public static bool ConstructDefaultPrefix(ref SaveManager __result)
    {
        PatchHelper.Log(
            $"[Cloud] ConstructDefaultPrefix called. Session={Session != null}, CloudSync={CloudSyncEnabled}"
        );

        if (!CloudSyncEnabled)
        {
            PatchHelper.Log("[Cloud] Cloud sync disabled by user — using local-only SaveManager");
            return true;
        }

        if (Session?.UnifiedMessages == null)
        {
            PatchHelper.Log("[Cloud] No session/UnifiedMessages — using local-only SaveManager");
            return true;
        }

        try
        {
            var localStore = new GodotFileIo(UserDataPathProvider.GetAccountScopedBasePath(null));
            var cloudStore = new SteamKit2CloudSaveStore(Session.UnifiedMessages);
            var wrappedStore = new CloudSaveStore(localStore, cloudStore);

            __result = new SaveManager(wrappedStore);
            PatchHelper.Log("[Cloud] Created SaveManager with SteamKit2 cloud store");
            return false; // Skip original
        }
        catch (System.Exception ex)
        {
            PatchHelper.Log(
                $"[Cloud] Cloud store injection failed, falling back to local: {ex.Message}"
            );
            return true; // Let original run
        }
    }

    // Replaces the default sync with timestamp-aware conflict resolution.
    // Downloads only when cloud is strictly newer; keeps local otherwise.
    public static bool SyncCloudToLocalPrefix(
        CloudSaveStore __instance,
        string path,
        ref Task __result
    )
    {
        __result = SmartSyncCloudToLocal(__instance, path);
        return false;
    }

    private static async Task SmartSyncCloudToLocal(CloudSaveStore store, string path)
    {
        try
        {
            var cloud = store.CloudStore;
            var local = store.LocalStore;

            bool cloudExists = cloud.FileExists(path);
            bool localExists = local.FileExists(path);

            if (cloudExists && localExists)
            {
                var cloudTime = cloud.GetLastModifiedTime(path);
                var localTime = local.GetLastModifiedTime(path);

                if (cloudTime > localTime)
                {
                    // Cloud is newer → download to local
                    PatchHelper.Log(
                        $"[Cloud] Sync: downloading {path} (cloud={cloudTime:u} > local={localTime:u})"
                    );
                    string content = await cloud.ReadFileAsync(path);
                    await local.WriteFileAsync(path, content);
                    local.SetLastModifiedTime(path, cloudTime);
                }
                else if (localTime > cloudTime)
                {
                    // Local is newer → keep local, it will be uploaded via normal write flow
                    PatchHelper.Log(
                        $"[Cloud] Sync: keeping local {path} (local={localTime:u} > cloud={cloudTime:u})"
                    );
                }
                else
                {
                    PatchHelper.Log($"[Cloud] Sync: {path} up to date ({cloudTime:u})");
                }
            }
            else if (cloudExists)
            {
                // Only cloud has it → download
                var cloudTime = cloud.GetLastModifiedTime(path);
                PatchHelper.Log($"[Cloud] Sync: downloading new {path} from cloud ({cloudTime:u})");
                string content = await cloud.ReadFileAsync(path);
                await local.WriteFileAsync(path, content);
                local.SetLastModifiedTime(path, cloudTime);
            }
            else if (localExists)
            {
                // Local only. Could be a failed upload or a cloud deletion.
                // Keep local to prevent data loss; it will upload on next save.
                PatchHelper.Log(
                    $"[Cloud] Sync: {path} exists locally but not on cloud, keeping local"
                );
            }
        }
        catch (Exception ex)
        {
            // Isolate per-file failures so the rest of the sync can proceed.
            PatchHelper.Log($"[Cloud] Sync failed for {path}: {ex.Message}");
        }
    }

    private static async Task RunLauncherThenGame(object game)
    {
        var gameNode = (Node)game;

        var launcher = new LauncherUI();
        gameNode.AddChild(launcher);
        launcher.Initialize();
        PatchHelper.Log("Launcher UI displayed");

        var session = await launcher.WaitForLaunch();
        PatchHelper.Log(
            session != null
                ? "User launched game, proceeding to startup..."
                : "User launched game in offline mode..."
        );

        Session = session;

        // SaveManager may have been accessed during atlas loading before Session was
        // set. Reset it so ConstructDefault re-runs with cloud store injection.
        var instanceField = typeof(SaveManager).GetField(
            "_instance",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        if (instanceField != null)
        {
            instanceField.SetValue(null, null);
            PatchHelper.Log("[Cloud] Reset SaveManager._instance for cloud store re-injection");
        }

        launcher.QueueFree();

        // Force shader compilation on first launch to prevent VFX stutter.
        if (ShaderWarmupScreen.NeedsWarmup())
        {
            var warmup = new ShaderWarmupScreen();
            gameNode.AddChild(warmup);
            warmup.Initialize();
            await warmup.WaitForCompletion();
            warmup.QueueFree();
        }

        // Ensure settings exist before GameStartup runs, since deferred calls from
        // the early SaveManager would crash on null SettingsSave otherwise.
        SaveManager.Instance.InitSettingsData();

        // Invoke the original private GameStartup() via reflection
        var gameStartup = game.GetType()
            .GetMethod("GameStartup", BindingFlags.NonPublic | BindingFlags.Instance);

        try
        {
            await (Task)gameStartup.Invoke(game, null);
        }
        catch (TargetInvocationException ex)
        {
            PatchHelper.Log($"Game startup failed: {ex.InnerException?.Message}");
            throw ex.InnerException ?? ex;
        }
    }
}
