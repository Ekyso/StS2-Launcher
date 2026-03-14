using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    private int _retries;
    private DateTimeOffset _nextRetryTime = DateTimeOffset.MinValue;
    private readonly object _loadLock = new();

    public CloudFileCache(SteamConnection connection)
    {
        _connection = connection;
    }

    public static string CanonicalizePath(string path)
    {
        return path.Replace("user://", "").Replace("\\", "/");
    }

    public bool FileExists(string path)
    {
        EnsureLoaded();
        return _files.ContainsKey(CanonicalizePath(path));
    }

    public DateTimeOffset GetLastModifiedTime(string path)
    {
        EnsureLoaded();
        return _files.TryGetValue(CanonicalizePath(path), out var info)
            ? info.Timestamp
            : DateTimeOffset.MinValue;
    }

    public int GetFileSize(string path)
    {
        EnsureLoaded();
        return _files.TryGetValue(CanonicalizePath(path), out var info) ? info.Size : 0;
    }

    public bool HasCloudFiles()
    {
        EnsureLoaded();
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
        EnsureLoaded();

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
        EnsureLoaded();

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
        _retries = 0;
        _nextRetryTime = DateTimeOffset.MinValue;
        EnsureLoaded();
    }

    private void EnsureLoaded()
    {
        if (_loaded)
            return;
        if (_retries >= MaxRetries)
            return;
        if (_retries > 0 && DateTimeOffset.UtcNow < _nextRetryTime)
            return;

        lock (_loadLock)
        {
            if (_loaded || _retries >= MaxRetries)
                return;
            if (_retries > 0 && DateTimeOffset.UtcNow < _nextRetryTime)
                return;

            try
            {
                LoadFileList();
                _loaded = true;
            }
            catch (Exception ex)
            {
                _retries++;
                var backoffSeconds = Math.Pow(2, _retries);
                _nextRetryTime = DateTimeOffset.UtcNow.AddSeconds(backoffSeconds);
                PatchHelper.Log(
                    $"[Cloud] Failed to enumerate cloud files (attempt {_retries}/{MaxRetries}): {ex.Message}"
                );

                if (_retries >= MaxRetries)
                    PatchHelper.Log(
                        "[Cloud] Max retries reached for cloud file enumeration this session."
                    );
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
