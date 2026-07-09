using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using SteamKit2.Internal;

namespace STS2Mobile.Steam;

// In-memory cache of cloud file metadata (size, timestamp, persistence flag).
// Loaded lazily from Steam on first access with exponential backoff on failure.
public class CloudFileCache
{
    private const uint AppId = 2868840;
    private const int MaxRetries = 5;

    private readonly SteamConnection _connection;
    private readonly ConcurrentDictionary<string, CloudFileInfo> _files = new();
    private volatile bool _loaded;
    private readonly object _loadLock = new();

    public CloudFileCache(SteamConnection connection)
    {
        _connection = connection;
    }

    public static string CanonicalizePath(string path)
    {
        return path.Replace("user://", "").Replace("\\", "/");
    }

    // True once the cloud file list has been loaded (via the background Prewarm or an
    // explicit Refresh). Callers that must not act on a half-loaded list check this first.
    public bool IsLoaded => _loaded;

    // Query methods deliberately do NOT trigger a (blocking) network load: the list is
    // warmed on a background thread at store construction. On the render thread we must
    // never stall on the network, so an unwarmed cache simply reads as empty here — the
    // cloud sync is gated on IsLoaded (see SyncCloudToLocalPrefix) so it will not act
    // destructively on that empty view, and HasCloudFiles stays conservative below.
    public bool FileExists(string path)
    {
        return _files.ContainsKey(CanonicalizePath(path));
    }

    public DateTimeOffset GetLastModifiedTime(string path)
    {
        return _files.TryGetValue(CanonicalizePath(path), out var info)
            ? info.Timestamp
            : DateTimeOffset.MinValue;
    }

    public int GetFileSize(string path)
    {
        return _files.TryGetValue(CanonicalizePath(path), out var info) ? info.Size : 0;
    }

    public bool HasCloudFiles()
    {
        if (!_loaded)
            return true; // Assume cloud has files to prevent destructive sync
        return _files.Count > 0;
    }

    public void ForgetFile(string path)
    {
        if (_files.TryGetValue(CanonicalizePath(path), out var info))
            info.Persisted = false;
    }

    public bool IsFilePersisted(string path)
    {
        return _files.TryGetValue(CanonicalizePath(path), out var info) && info.Persisted;
    }

    public void Set(string path, int size, DateTimeOffset timestamp)
    {
        _files[CanonicalizePath(path)] = new CloudFileInfo { Size = size, Timestamp = timestamp };
    }

    public void Remove(string path)
    {
        _files.TryRemove(CanonicalizePath(path), out _);
    }

    public string[] GetFilesInDirectory(string directoryPath)
    {
        directoryPath = CanonicalizePath(directoryPath);

        var prefix = directoryPath.Length > 0 ? directoryPath + "/" : "";
        var result = new List<string>();

        foreach (var key in _files.Keys)
        {
            if (key.StartsWith(prefix) && key.Length > prefix.Length)
            {
                var remainder = key.Substring(prefix.Length);
                if (!remainder.Contains('/') && !remainder.Contains('\\'))
                    result.Add(remainder);
            }
        }

        return result.ToArray();
    }

    public string[] GetDirectoriesInDirectory(string directoryPath)
    {
        directoryPath = CanonicalizePath(directoryPath);

        var prefix = directoryPath.Length > 0 ? directoryPath + "/" : "";
        var dirs = new HashSet<string>();

        foreach (var key in _files.Keys)
        {
            if (key.StartsWith(prefix) && key.Length > prefix.Length)
            {
                var remainder = key.Substring(prefix.Length);
                var slashIndex = remainder.IndexOf('/');
                if (slashIndex >= 0)
                    dirs.Add(remainder.Substring(0, slashIndex));
            }
        }

        return [.. dirs];
    }

    public void Refresh()
    {
        _files.Clear();
        _loaded = false;
        Prewarm();
    }

    // Loads the cloud file list, retrying a bounded number of times with a short backoff.
    // MUST run on a background thread (store construction / manual push-pull) — it is the
    // ONLY path that enumerates now, since queries no longer load lazily on the render
    // thread. Bounded so a dead connection can't churn for minutes; a transient startup
    // blip still recovers because we retry here instead of failing the whole session.
    public void Prewarm()
    {
        for (int attempt = 1; attempt <= MaxRetries && !_loaded; attempt++)
        {
            if (TryLoadOnce())
                return;
            if (attempt < MaxRetries)
                Thread.Sleep(Math.Min(1000 * attempt, 5000)); // 1s,2s,3s,4s (capped 5s)
        }

        if (!_loaded)
            PatchHelper.Log(
                $"[Cloud] Could not enumerate cloud files after {MaxRetries} attempts; "
                    + "cloud sync will use local this session and retry next launch."
            );
    }

    private bool TryLoadOnce()
    {
        if (_loaded)
            return true;

        lock (_loadLock)
        {
            if (_loaded)
                return true;

            try
            {
                LoadFileList();
                _loaded = true;
                return true;
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"[Cloud] Failed to enumerate cloud files: {ex.Message}");
                return false;
            }
        }
    }

    private void LoadFileList()
    {
        uint startIndex = 0;
        const uint pageSize = 500;

        while (true)
        {
            var result = _connection
                .SendCloud<CCloud_EnumerateUserFiles_Request, CCloud_EnumerateUserFiles_Response>(
                    "EnumerateUserFiles",
                    new CCloud_EnumerateUserFiles_Request
                    {
                        appid = AppId,
                        start_index = startIndex,
                        count = pageSize,
                    }
                )
                .GetAwaiter()
                .GetResult();

            if (result.files == null || result.files.Count == 0)
                break;

            foreach (var file in result.files)
            {
                _files[file.filename] = new CloudFileInfo
                {
                    Size = (int)file.file_size,
                    Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)file.timestamp),
                };
            }

            startIndex += (uint)result.files.Count;
            if (result.files.Count < pageSize)
                break;
        }

        PatchHelper.Log($"[Cloud] Enumerated {_files.Count} cloud files");
    }

    private class CloudFileInfo
    {
        public int Size;
        public DateTimeOffset Timestamp;
        public volatile bool Persisted = true;
    }
}
