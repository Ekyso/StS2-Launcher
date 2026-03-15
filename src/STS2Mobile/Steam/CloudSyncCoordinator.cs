using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace STS2Mobile.Steam;

// Stateless cloud sync coordinator: auto sync, manual push/pull, and save backups.
public static class CloudSyncCoordinator
{
    private const int MaxBackups = 50;
    private const int HistoryFileLimit = 100;

    internal static bool LocalBackupEnabled;

    public static async Task PushFileAsync(ISaveStore local, ICloudSaveStore cloud, string path)
    {
        if (!local.FileExists(path))
            return;

        string content = local.ReadFile(path);

        if (cloud.FileExists(path))
        {
            string cloudContent = await cloud.ReadFileAsync(path);
            if (content == cloudContent)
            {
                PatchHelper.Log($"[Cloud] Push: skipping {path} (identical)");
                return;
            }
            BackupProgressContent(path, cloudContent, "cloud");
        }

        cloud.WriteFile(path, content);
        PatchHelper.Log($"[Cloud] Push: uploaded {path}");
    }

    public static async Task PullFileAsync(ISaveStore local, ICloudSaveStore cloud, string path)
    {
        if (!cloud.FileExists(path))
            return;

        string cloudContent = await cloud.ReadFileAsync(path);

        if (local.FileExists(path))
        {
            string localContent = local.ReadFile(path);
            if (localContent == cloudContent)
            {
                PatchHelper.Log($"[Cloud] Pull: skipping {path} (identical)");
                return;
            }
            BackupProgressFile(local, path);
        }

        var pullTime = cloud.GetLastModifiedTime(path);
        await local.WriteFileAsync(path, cloudContent);
        local.SetLastModifiedTime(path, pullTime);
        PatchHelper.Log($"[Cloud] Pull: downloaded {path}");
    }

    // Uses content comparison only — timestamps are unreliable on mobile (game init
    // rewrites files, OS touches metadata). Progress/run files use SaveProgressComparer;
    // non-progress files default to cloud wins; history files sync bidirectionally.
    public static async Task AutoSyncFileAsync(ISaveStore local, ICloudSaveStore cloud, string path)
    {
        try
        {
            bool cloudExists = cloud.FileExists(path);
            bool localExists = local.FileExists(path);

            if (cloudExists && localExists)
            {
                string localContent = local.ReadFile(path);
                string cloudContent = await cloud.ReadFileAsync(path);

                if (IsCorrupt(localContent))
                {
                    PatchHelper.Log($"[Cloud] Sync: local {path} is corrupt, pulling from cloud");
                    BackupProgressFile(local, path);
                    var cloudTime = cloud.GetLastModifiedTime(path);
                    await local.WriteFileAsync(path, cloudContent);
                    local.SetLastModifiedTime(path, cloudTime);
                    return;
                }

                if (localContent == cloudContent)
                {
                    PatchHelper.Log($"[Cloud] Sync: {path} identical, skipping");
                    return;
                }

                var result = SaveProgressComparer.Compare(path, localContent, cloudContent);

                if (result == CompareResult.CloudWins)
                {
                    PatchHelper.Log($"[Cloud] Sync: cloud wins for {path}");
                    BackupProgressFile(local, path);
                    var cloudTime = cloud.GetLastModifiedTime(path);
                    await local.WriteFileAsync(path, cloudContent);
                    local.SetLastModifiedTime(path, cloudTime);
                }
                else if (result == CompareResult.LocalWins)
                {
                    PatchHelper.Log($"[Cloud] Sync: local wins for {path}, uploading");
                    BackupProgressContent(path, cloudContent, "cloud");
                    cloud.WriteFile(path, localContent);
                }
                else
                {
                    // Cloud wins on equal progress or non-progress files to preserve PC as primary.
                    PatchHelper.Log($"[Cloud] Sync: contents differ for {path}, cloud wins");
                    BackupProgressFile(local, path);
                    var cloudTime = cloud.GetLastModifiedTime(path);
                    await local.WriteFileAsync(path, cloudContent);
                    local.SetLastModifiedTime(path, cloudTime);
                }
            }
            else if (cloudExists)
            {
                await PullFileAsync(local, cloud, path);
            }
            else if (localExists)
            {
                await PushFileAsync(local, cloud, path);
            }
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Cloud] Sync failed for {path}: {ex.Message}");
        }
    }

    public static async Task ManualPushAllAsync(string accountName, string refreshToken)
    {
        var localStore = new GodotFileIo(UserDataPathProvider.GetAccountScopedBasePath(null));
        var cloudStore =
            SteamKit2CloudSaveStore.Instance
            ?? new SteamKit2CloudSaveStore(accountName, refreshToken);

        var paths = GetSaveFilePaths(localStore);
        PatchHelper.Log($"[Cloud] Push: starting ({paths.Count} files)");

        int backedUp = 0;
        foreach (var path in paths)
        {
            try
            {
                if (!IsImportantSave(path) || !cloudStore.FileExists(path))
                    continue;

                PatchHelper.Log($"[Cloud] Push: backing up cloud {path}");
                var content = await cloudStore.ReadFileAsync(path);
                BackupSaveContent(path, content, "cloud-pre-push");
                backedUp++;
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"[Cloud] Push: backup failed for cloud {path}: {ex.Message}");
            }
        }
        if (backedUp > 0)
            PatchHelper.Log($"[Cloud] Push: backed up {backedUp} cloud files");

        cloudStore.BeginSaveBatch();
        int count = 0;
        foreach (var path in paths)
        {
            try
            {
                if (!localStore.FileExists(path))
                    continue;

                string content = localStore.ReadFile(path);
                PatchHelper.Log($"[Cloud] Push: queuing {path} ({content.Length} bytes)");
                cloudStore.WriteFile(path, content);
                count++;
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"[Cloud] Push: failed for {path}: {ex.Message}");
            }
        }
        cloudStore.EndSaveBatch();

        PatchHelper.Log($"[Cloud] Push complete: {count} files batched for upload");
    }

    public static async Task ManualPullAllAsync(string accountName, string refreshToken)
    {
        var localStore = new GodotFileIo(UserDataPathProvider.GetAccountScopedBasePath(null));
        var cloudStore =
            SteamKit2CloudSaveStore.Instance
            ?? new SteamKit2CloudSaveStore(accountName, refreshToken);

        var paths = GetSaveFilePaths(cloudStore);
        PatchHelper.Log($"[Cloud] Pull: starting ({paths.Count} files)");

        int backedUp = 0;
        foreach (var path in paths)
        {
            try
            {
                if (!localStore.FileExists(path))
                    continue;

                BackupSaveContent(path, localStore.ReadFile(path), "local-pre-pull");
                backedUp++;
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"[Cloud] Pull: backup failed for local {path}: {ex.Message}");
            }
        }
        if (backedUp > 0)
            PatchHelper.Log($"[Cloud] Pull: backed up {backedUp} local files");

        int downloaded = 0;
        int skipped = 0;
        foreach (var path in paths)
        {
            try
            {
                if (!cloudStore.FileExists(path))
                {
                    skipped++;
                    continue;
                }
                PatchHelper.Log($"[Cloud] Pull: downloading {path}");
                var pullTime = cloudStore.GetLastModifiedTime(path);
                string content = await cloudStore.ReadFileAsync(path);
                await localStore.WriteFileAsync(path, content);
                localStore.SetLastModifiedTime(path, pullTime);
                PatchHelper.Log($"[Cloud] Pull: wrote {path} ({content.Length} bytes)");
                downloaded++;
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"[Cloud] Pull: failed for {path}: {ex.Message}");
            }
        }

        PatchHelper.Log($"[Cloud] Pull complete: {downloaded} downloaded, {skipped} not in cloud");
    }

    public static List<string> GetSaveFilePaths(ISaveStore store)
    {
        var paths = new List<string>();
        CollectProfilePaths(paths, store.GetFilesInDirectory, store.DirectoryExists);
        return paths;
    }

    public static List<string> GetSaveFilePaths(ICloudSaveStore store)
    {
        var paths = new List<string>();
        CollectProfilePaths(paths, store.GetFilesInDirectory, store.DirectoryExists);
        return paths;
    }

    // Collects save paths for both vanilla and modded profile directories.
    private static void CollectProfilePaths(
        List<string> paths,
        Func<string, string[]> getFiles,
        Func<string, bool> dirExists
    )
    {
        var wasModded = UserDataPathProvider.IsRunningModded;
        try
        {
            foreach (bool modded in new[] { false, true })
            {
                UserDataPathProvider.IsRunningModded = modded;
                for (int i = 1; i <= 3; i++)
                {
                    paths.Add(ProgressSaveManager.GetProgressPathForProfile(i));
                    paths.Add(RunSaveManager.GetRunSavePath(i, "current_run.save"));
                    paths.Add(RunSaveManager.GetRunSavePath(i, "current_run_mp.save"));
                    paths.Add(PrefsSaveManager.GetPrefsPath(i));
                    AddHistoryFiles(paths, getFiles, dirExists, i);
                }
            }
        }
        finally
        {
            UserDataPathProvider.IsRunningModded = wasModded;
        }
    }

    private static void AddHistoryFiles(
        List<string> paths,
        Func<string, string[]> getFiles,
        Func<string, bool> dirExists,
        int profileId
    )
    {
        var historyDir = RunHistorySaveManager.GetHistoryPath(profileId);
        if (!dirExists(historyDir))
            return;

        var runFiles = getFiles(historyDir)
            .Where(f => f.EndsWith(".run") && !f.EndsWith(".backup") && !f.EndsWith(".tmp"))
            .OrderByDescending(f => f) // Filenames are Unix timestamps — descending = newest first
            .Take(HistoryFileLimit);

        foreach (var file in runFiles)
            paths.Add($"{historyDir}/{file}");
    }

    // Save files are JSON; a non-JSON opener indicates corruption (e.g., unencrypted write).
    private static bool IsCorrupt(string content)
    {
        if (string.IsNullOrEmpty(content))
            return false;
        return content[0] != '{' && content[0] != '[';
    }

    // History files are immutable past runs and don't need backup.
    private static bool IsImportantSave(string path)
    {
        var lower = path.Replace("user://", "").ToLowerInvariant();
        return lower.Contains("progress.save")
            || lower.Contains("current_run")
            || lower.Contains("prefs");
    }

    public static void BackupSaveContent(string path, string content, string source)
    {
        try
        {
            if (string.IsNullOrEmpty(content))
                return;

            if (!LocalBackupEnabled || !AppPaths.HasStoragePermission())
                return;

            var canonPath = path.Replace("user://", "").Replace("\\", "/");
            var parts = canonPath.Split('/');

            var profileDir = "default";
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].StartsWith("profile"))
                {
                    profileDir = parts[i];
                    break;
                }
            }

            var fileName = Path.GetFileName(canonPath);
            var backupDir = Path.Combine(AppPaths.ExternalSaveBackupsDir, profileDir);
            Directory.CreateDirectory(backupDir);

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var backupPath = Path.Combine(backupDir, $"{fileName}.{timestamp}.{source}.bak");

            File.WriteAllText(backupPath, content);
            PatchHelper.Log($"[Cloud] Backed up {source} {path}");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Cloud] Backup failed for {source} {path}: {ex.Message}");
        }
    }

    public static void BackupProgressFile(ISaveStore local, string path)
    {
        var canonPath = path.Replace("user://", "").Replace("\\", "/").ToLowerInvariant();
        if (!canonPath.Contains("progress") || !canonPath.EndsWith(".save"))
            return;

        if (!local.FileExists(path))
            return;

        BackupProgressContent(path, local.ReadFile(path), "local");
    }

    public static void BackupProgressContent(string path, string content, string source)
    {
        try
        {
            var canonPath = path.Replace("user://", "").Replace("\\", "/").ToLowerInvariant();
            if (!canonPath.Contains("progress") || !canonPath.EndsWith(".save"))
                return;

            if (!LocalBackupEnabled || !AppPaths.HasStoragePermission())
                return;

            var parts = canonPath.Split('/');
            var profileDir = "default";
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].StartsWith("profile"))
                {
                    profileDir = parts[i];
                    break;
                }
            }

            var backupDir = Path.Combine(AppPaths.ExternalSaveBackupsDir, profileDir);
            Directory.CreateDirectory(backupDir);

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var backupPath = Path.Combine(backupDir, $"progress.save.{timestamp}.{source}.bak");

            File.WriteAllText(backupPath, content);
            PatchHelper.Log($"[Cloud] Backed up {source} {path} → {backupPath}");

            var backups = Directory
                .GetFiles(backupDir, "progress.save.*.bak")
                .OrderByDescending(f => f)
                .Skip(MaxBackups)
                .ToArray();

            foreach (var old in backups)
            {
                try
                {
                    File.Delete(old);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Cloud] Backup failed for {source} {path}: {ex.Message}");
        }
    }
}
