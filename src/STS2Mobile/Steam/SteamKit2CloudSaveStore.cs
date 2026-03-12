using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Saves;
using SteamKit2;
using SteamKit2.Internal;

namespace STS2Mobile.Steam;

// Implements ICloudSaveStore using SteamKit2 CCloud unified messages instead of
// the Steamworks SDK. All writes are queued to a background thread to avoid
// mid-game stutters. File metadata is cached in memory after initial enumeration.
public class SteamKit2CloudSaveStore : ICloudSaveStore, ISaveStore
{
    private const uint AppId = 2868840;

    private readonly SteamUnifiedMessages _unifiedMessages;
    private readonly HttpClient _http = new();

    private readonly ConcurrentDictionary<string, CloudFileInfo> _fileCache = new();
    private volatile bool _cacheLoaded;
    private volatile bool _cacheFailed;
    private int _cacheRetries;
    private readonly object _cacheLock = new();

    private volatile bool _collectingBatch;
    private readonly List<(string path, byte[] bytes)> _batchPendingFiles = new();

    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private byte[] _encryptionKey;
    private bool _encryptionKeyFetched;

    private readonly BlockingCollection<Action> _writeQueue = new();
    private readonly Thread _writeThread;

    public SteamKit2CloudSaveStore(SteamUnifiedMessages unifiedMessages)
    {
        _unifiedMessages = unifiedMessages;

        // Required for CCloud RPC responses to be routed back correctly.
        _unifiedMessages.CreateService<SteamKit2.Internal.Cloud>();

        _writeThread = new Thread(ProcessWriteQueue)
        {
            IsBackground = true,
            Name = "CloudSaveWriter",
        };
        _writeThread.Start();
    }

    private void ProcessWriteQueue()
    {
        foreach (var action in _writeQueue.GetConsumingEnumerable())
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"[Cloud] Background write failed: {ex.Message}");
            }
        }
    }

    // Sends a CCloud RPC. Serializes calls because WebSocket sends are not thread-safe.
    private async Task<TResult> SendCloud<TRequest, TResult>(string method, TRequest request)
        where TRequest : ProtoBuf.IExtensible, new()
        where TResult : ProtoBuf.IExtensible, new()
    {
        await _sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var job = _unifiedMessages.SendMessage<TRequest, TResult>($"Cloud.{method}#1", request);
            var response = await job.ToTask().ConfigureAwait(false);
            if (response.Result != EResult.OK)
                throw new InvalidOperationException($"Cloud.{method} failed: {response.Result}");
            return response.Body;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task<byte[]> GetEncryptionKey()
    {
        if (_encryptionKeyFetched && _encryptionKey != null)
            return _encryptionKey;

        var result = await SendCloud<
            CCloud_GetClientEncryptionKey_Request,
            CCloud_GetClientEncryptionKey_Response
        >("GetClientEncryptionKey", new CCloud_GetClientEncryptionKey_Request())
            .ConfigureAwait(false);

        _encryptionKey =
            result.key
            ?? throw new InvalidOperationException("GetClientEncryptionKey returned null key");
        _encryptionKeyFetched = true;
        PatchHelper.Log($"[Cloud] Got encryption key ({_encryptionKey.Length} bytes)");
        return _encryptionKey;
    }

    public string ReadFile(string path)
    {
        return ReadFileAsync(path).GetAwaiter().GetResult();
    }

    public async Task<string> ReadFileAsync(string path)
    {
        path = CanonicalizePath(path);
        EnsureCacheLoaded();

        if (!_fileCache.TryGetValue(path, out var cached))
            throw new FileNotFoundException($"Cloud file not found: {path}");

        if (cached.Size == 0)
            return string.Empty;

        var result = await SendCloud<
            CCloud_ClientFileDownload_Request,
            CCloud_ClientFileDownload_Response
        >(
                "ClientFileDownload",
                new CCloud_ClientFileDownload_Request { appid = AppId, filename = path }
            )
            .ConfigureAwait(false);

        if (result.appid != AppId || string.IsNullOrEmpty(result.url_host))
            throw new InvalidOperationException($"Cloud download failed for {path}");

        var scheme = result.use_https ? "https" : "http";
        var url = $"{scheme}://{result.url_host}{result.url_path}";

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var header in result.request_headers)
            httpRequest.Headers.TryAddWithoutValidation(header.name, header.value);

        var httpResponse = await _http.SendAsync(httpRequest).ConfigureAwait(false);
        httpResponse.EnsureSuccessStatusCode();
        var data = await httpResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

        // Steamworks SDK clients encrypt uploads; SteamKit2 downloads are raw.
        if (result.encrypted)
        {
            var key = await GetEncryptionKey().ConfigureAwait(false);
            data = CryptoHelper.SymmetricDecrypt(data, key);
            PatchHelper.Log($"[Cloud] Decrypted {path} ({result.file_size} → {data.Length} bytes)");
        }

        return Encoding.UTF8.GetString(data);
    }

    public void WriteFile(string path, string content)
    {
        WriteFile(path, Encoding.UTF8.GetBytes(content));
    }

    // Updates cache immediately, then queues the upload on the background thread.
    // Timestamps are truncated to Unix seconds to match Steam's precision.
    public void WriteFile(string path, byte[] bytes)
    {
        var canonPath = CanonicalizePath(path);
        var truncatedNow = DateTimeOffset.FromUnixTimeSeconds(
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        );
        _fileCache[canonPath] = new CloudFileInfo { Size = bytes.Length, Timestamp = truncatedNow };

        if (_collectingBatch)
        {
            _batchPendingFiles.Add((path, bytes));
        }
        else
        {
            var ts = truncatedNow;
            _writeQueue.Add(() => UploadWithRetry(path, bytes, timestamp: ts));
        }
    }

    private void UploadWithRetry(
        string path,
        byte[] bytes,
        ulong batchId = 0,
        DateTimeOffset? timestamp = null
    )
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                WriteFileAsync(path, bytes, batchId, timestamp).GetAwaiter().GetResult();
                return;
            }
            catch (InvalidOperationException ex)
                when (ex.Message.Contains("TooManyPending") && attempt < 2)
            {
                PatchHelper.Log(
                    $"[Cloud] Upload throttled for {CanonicalizePath(path)}, retrying in {(attempt + 1) * 2}s..."
                );
                Thread.Sleep((attempt + 1) * 2000);
            }
            catch (Exception ex)
            {
                PatchHelper.Log(
                    $"[Cloud] Upload failed for {CanonicalizePath(path)}: {ex.Message}"
                );
                return;
            }
        }
    }

    public Task WriteFileAsync(string path, string content)
    {
        WriteFile(path, content);
        return Task.CompletedTask;
    }

    public Task WriteFileAsync(string path, byte[] bytes)
    {
        WriteFile(path, bytes);
        return Task.CompletedTask;
    }

    private async Task WriteFileAsync(
        string path,
        byte[] bytes,
        ulong batchId,
        DateTimeOffset? timestamp = null
    )
    {
        path = CanonicalizePath(path);

        var fileHash = System.Security.Cryptography.SHA1.HashData(bytes);

        var uploadTimestamp = timestamp.HasValue
            ? (ulong)timestamp.Value.ToUnixTimeSeconds()
            : (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var beginRequest = new CCloud_ClientBeginFileUpload_Request
        {
            appid = AppId,
            filename = path,
            file_size = (uint)bytes.Length,
            raw_file_size = (uint)bytes.Length,
            file_sha = fileHash,
            time_stamp = uploadTimestamp,
            can_encrypt = false,
            is_shared_file = false,
        };

        if (batchId != 0)
            beginRequest.upload_batch_id = batchId;

        CCloud_ClientBeginFileUpload_Response beginResult;
        try
        {
            beginResult = await SendCloud<
                CCloud_ClientBeginFileUpload_Request,
                CCloud_ClientBeginFileUpload_Response
            >("ClientBeginFileUpload", beginRequest)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("DuplicateRequest"))
        {
            PatchHelper.Log($"[Cloud] Skipped upload for {path} (already up to date)");
            return;
        }

        // Must always commit after begin, even on failure, to clear the pending state.
        bool uploadSucceeded = false;
        try
        {
            if (beginResult.encrypt_file)
                PatchHelper.Log(
                    $"[Cloud] Warning: Steam requested encryption for {path}, uploading unencrypted"
                );

            foreach (var block in beginResult.block_requests)
            {
                var scheme = block.use_https ? "https" : "http";
                var url = $"{scheme}://{block.url_host}{block.url_path}";

                var method = block.http_method == 2 ? HttpMethod.Post : HttpMethod.Put;
                var request = new HttpRequestMessage(method, url);

                byte[] bodyData =
                    block.explicit_body_data?.Length > 0
                        ? block.explicit_body_data
                        : bytes[
                            (int)block.block_offset..(
                                (int)block.block_offset + (int)block.block_length
                            )
                        ];

                request.Content = new ByteArrayContent(bodyData);
                request.Content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                request.Content.Headers.ContentLength = bodyData.Length;

                foreach (var header in block.request_headers)
                    request.Headers.TryAddWithoutValidation(header.name, header.value);

                var httpResponse = await _http.SendAsync(request).ConfigureAwait(false);
                httpResponse.EnsureSuccessStatusCode();
            }

            uploadSucceeded = true;
        }
        finally
        {
            try
            {
                var commitResult = await SendCloud<
                    CCloud_ClientCommitFileUpload_Request,
                    CCloud_ClientCommitFileUpload_Response
                >(
                        "ClientCommitFileUpload",
                        new CCloud_ClientCommitFileUpload_Request
                        {
                            transfer_succeeded = uploadSucceeded,
                            appid = AppId,
                            file_sha = fileHash,
                            filename = path,
                        }
                    )
                    .ConfigureAwait(false);

                if (uploadSucceeded && !commitResult.file_committed)
                    PatchHelper.Log($"[Cloud] Commit returned file_committed=false for {path}");
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"[Cloud] Commit failed for {path}: {ex.Message}");
            }
        }

        if (!uploadSucceeded)
            throw new InvalidOperationException($"Cloud upload failed for {path}");

        PatchHelper.Log($"[Cloud] Wrote {bytes.Length} bytes to {path}");
    }

    public bool FileExists(string path)
    {
        path = CanonicalizePath(path);
        EnsureCacheLoaded();
        return _fileCache.ContainsKey(path);
    }

    public bool DirectoryExists(string path)
    {
        return true;
    }

    public void DeleteFile(string path)
    {
        var canonPath = CanonicalizePath(path);
        _fileCache.TryRemove(canonPath, out _);

        _writeQueue.Add(() =>
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    SendCloud<CCloud_ClientDeleteFile_Request, CCloud_ClientDeleteFile_Response>(
                            "ClientDeleteFile",
                            new CCloud_ClientDeleteFile_Request
                            {
                                appid = AppId,
                                filename = canonPath,
                            }
                        )
                        .GetAwaiter()
                        .GetResult();
                    break;
                }
                catch (InvalidOperationException ex)
                    when (ex.Message.Contains("TooManyPending") && attempt < 2)
                {
                    PatchHelper.Log($"[Cloud] Delete throttled for {canonPath}, retrying...");
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    PatchHelper.Log($"[Cloud] Delete failed for {canonPath}: {ex.Message}");
                    break;
                }
            }
        });
    }

    public void RenameFile(string sourcePath, string destinationPath)
    {
        var content = ReadFile(sourcePath);
        WriteFile(destinationPath, content);
        DeleteFile(sourcePath);
    }

    public string[] GetFilesInDirectory(string directoryPath)
    {
        directoryPath = CanonicalizePath(directoryPath);
        EnsureCacheLoaded();

        var prefix = directoryPath.Length > 0 ? directoryPath + "/" : "";
        var result = new List<string>();

        foreach (var key in _fileCache.Keys)
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
        throw new NotImplementedException();
    }

    public void CreateDirectory(string directoryPath) { }

    public void DeleteDirectory(string directoryPath) { }

    public void DeleteTemporaryFiles(string directoryPath) { }

    public DateTimeOffset GetLastModifiedTime(string path)
    {
        path = CanonicalizePath(path);
        EnsureCacheLoaded();
        return _fileCache.TryGetValue(path, out var info)
            ? info.Timestamp
            : DateTimeOffset.MinValue;
    }

    public int GetFileSize(string path)
    {
        path = CanonicalizePath(path);
        EnsureCacheLoaded();
        return _fileCache.TryGetValue(path, out var info) ? info.Size : 0;
    }

    public void SetLastModifiedTime(string path, DateTimeOffset time)
    {
        throw new NotImplementedException();
    }

    public string GetFullPath(string filename)
    {
        throw new NotImplementedException();
    }

    public bool HasCloudFiles()
    {
        EnsureCacheLoaded();
        // Return true on failure to prevent SyncCloudToLocal from deleting local saves.
        if (_cacheFailed)
            return true;
        return _fileCache.Count > 0;
    }

    // Marks the file as not persisted (excluded from quota) without deleting data.
    public void ForgetFile(string path)
    {
        path = CanonicalizePath(path);
        if (_fileCache.TryGetValue(path, out var info))
            info.Persisted = false;
    }

    public bool IsFilePersisted(string path)
    {
        path = CanonicalizePath(path);
        return _fileCache.TryGetValue(path, out var info) && info.Persisted;
    }

    public void BeginSaveBatch()
    {
        _collectingBatch = true;
        _batchPendingFiles.Clear();
    }

    // Queues all collected files as a single batch upload.
    public void EndSaveBatch()
    {
        _collectingBatch = false;

        if (_batchPendingFiles.Count == 0)
            return;

        var files = new List<(string path, byte[] bytes)>(_batchPendingFiles);
        _batchPendingFiles.Clear();

        _writeQueue.Add(() =>
        {
            ulong batchId = 0;
            try
            {
                var request = new CCloud_BeginAppUploadBatch_Request
                {
                    appid = AppId,
                    machine_name = "android",
                };
                foreach (var (path, _) in files)
                    request.files_to_upload.Add(CanonicalizePath(path));

                var result = SendCloud<
                    CCloud_BeginAppUploadBatch_Request,
                    CCloud_BeginAppUploadBatch_Response
                >("BeginAppUploadBatch", request)
                    .GetAwaiter()
                    .GetResult();
                batchId = result.batch_id;
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"[Cloud] BeginSaveBatch failed: {ex.Message}");
                foreach (var (path, bytes) in files)
                    UploadWithRetry(path, bytes);
                return;
            }

            foreach (var (path, bytes) in files)
                UploadWithRetry(path, bytes, batchId);

            try
            {
                SendCloud<
                    CCloud_CompleteAppUploadBatch_Request,
                    CCloud_CompleteAppUploadBatch_Response
                >(
                        "CompleteAppUploadBatchBlocking",
                        new CCloud_CompleteAppUploadBatch_Request
                        {
                            appid = AppId,
                            batch_id = batchId,
                            batch_eresult = (uint)EResult.OK,
                        }
                    )
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"[Cloud] EndSaveBatch failed: {ex.Message}");
            }
        });
    }

    private static string CanonicalizePath(string path)
    {
        return path.Replace("user://", "").Replace("\\", "/");
    }

    private void EnsureCacheLoaded()
    {
        if (_cacheLoaded || _cacheFailed)
            return;

        lock (_cacheLock)
        {
            if (_cacheLoaded || _cacheFailed)
                return;

            try
            {
                LoadFileList();
                _cacheLoaded = true;
            }
            catch (Exception ex)
            {
                _cacheRetries++;
                PatchHelper.Log(
                    $"[Cloud] Failed to enumerate cloud files (attempt {_cacheRetries}): {ex.Message}"
                );

                if (_cacheRetries >= 2)
                {
                    _cacheFailed = true;
                    PatchHelper.Log(
                        "[Cloud] Giving up on cloud file enumeration. Cloud operations will be skipped."
                    );
                }
            }
        }
    }

    private void LoadFileList()
    {
        uint startIndex = 0;
        const uint pageSize = 500;

        while (true)
        {
            var result = SendCloud<
                CCloud_EnumerateUserFiles_Request,
                CCloud_EnumerateUserFiles_Response
            >(
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
                _fileCache[file.filename] = new CloudFileInfo
                {
                    Size = (int)file.file_size,
                    Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)file.timestamp),
                };
            }

            startIndex += (uint)result.files.Count;
            if (result.files.Count < pageSize)
                break;
        }

        PatchHelper.Log($"[Cloud] Enumerated {_fileCache.Count} cloud files");
    }

    // Clears and reloads the file list from Steam. Call after initial login.
    public void RefreshCache()
    {
        _fileCache.Clear();
        _cacheLoaded = false;
        _cacheFailed = false;
        _cacheRetries = 0;
        EnsureCacheLoaded();
    }

    private class CloudFileInfo
    {
        public int Size;
        public DateTimeOffset Timestamp;
        public volatile bool Persisted = true;
    }
}
