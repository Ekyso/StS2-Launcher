using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Saves;
using SteamKit2.Internal;

namespace STS2Mobile.Steam;

// ICloudSaveStore backed by SteamKit2 CCloud unified messages.
public class SteamKit2CloudSaveStore : ICloudSaveStore, ISaveStore, IDisposable
{
    private const uint AppId = 2868840;

    internal static SteamKit2CloudSaveStore Instance { get; private set; }

    private readonly SteamConnection _connection;
    private readonly CloudFileCache _cache;
    private readonly CloudWriteQueue _writeQueue;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private volatile bool _collectingBatch;
    private readonly List<(string path, byte[] bytes)> _batchPendingFiles = new();
    private readonly object _batchLock = new();

    public SteamKit2CloudSaveStore(string accountName, string refreshToken)
    {
        _connection = new SteamConnection(accountName, refreshToken);
        _cache = new CloudFileCache(_connection);
        _writeQueue = new CloudWriteQueue();

        Instance = this;
    }

    public void Flush(int timeoutMs = 5000)
    {
        _writeQueue.Flush(timeoutMs);
        _connection.Flush();
    }

    public void Dispose()
    {
        _writeQueue.Dispose();
        _connection.Dispose();
        _http.Dispose();
        if (Instance == this)
            Instance = null;
    }

    public string ReadFile(string path)
    {
        return ReadFileAsync(path).GetAwaiter().GetResult();
    }

    public async Task<string> ReadFileAsync(string path)
    {
        path = CloudFileCache.CanonicalizePath(path);

        if (!_cache.FileExists(path))
            throw new FileNotFoundException($"Cloud file not found: {path}");

        if (_cache.GetFileSize(path) == 0)
            return string.Empty;

        var result = await _connection
            .SendCloud<CCloud_ClientFileDownload_Request, CCloud_ClientFileDownload_Response>(
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

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var httpResponse = await _http.SendAsync(httpRequest, cts.Token).ConfigureAwait(false);
        httpResponse.EnsureSuccessStatusCode();
        var data = await httpResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        PatchHelper.Log(
            $"[Cloud] Downloaded {path} ({data.Length} bytes, encrypted={result.encrypted}, "
                + $"file_size={result.file_size}, raw_file_size={result.raw_file_size})"
        );

        // Only decompress if ZIP magic header present (PK\x03\x04).
        if (
            result.raw_file_size > 0
            && result.raw_file_size != result.file_size
            && data.Length >= 4
            && data[0] == 0x50
            && data[1] == 0x4B
            && data[2] == 0x03
            && data[3] == 0x04
        )
        {
            var compressedSize = data.Length;
            data = CloudCompression.Decompress(data);
            PatchHelper.Log($"[Cloud] Unzipped {path} ({compressedSize} → {data.Length} bytes)");
        }

        return Encoding.UTF8.GetString(data);
    }

    public void WriteFile(string path, string content)
    {
        WriteFile(path, Encoding.UTF8.GetBytes(content));
    }

    public void WriteFile(string path, byte[] bytes)
    {
        var canonPath = CloudFileCache.CanonicalizePath(path);
        var truncatedNow = DateTimeOffset.FromUnixTimeSeconds(
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        );
        _cache.Set(canonPath, bytes.Length, truncatedNow);

        lock (_batchLock)
        {
            if (_collectingBatch)
            {
                _batchPendingFiles.Add((path, bytes));
                return;
            }
        }

        var ts = truncatedNow;
        _writeQueue.Enqueue(() => UploadWithRetry(path, bytes, timestamp: ts));
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

    public bool FileExists(string path) => _cache.FileExists(path);

    public bool DirectoryExists(string path) => true;

    public void DeleteFile(string path)
    {
        var canonPath = CloudFileCache.CanonicalizePath(path);
        _cache.Remove(canonPath);

        _writeQueue.Enqueue(() =>
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    _connection
                        .SendCloud<
                            CCloud_ClientDeleteFile_Request,
                            CCloud_ClientDeleteFile_Response
                        >(
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
        try
        {
            DeleteFile(sourcePath);
        }
        catch (Exception ex)
        {
            PatchHelper.Log(
                $"[Cloud] RenameFile: delete of {CloudFileCache.CanonicalizePath(sourcePath)} "
                    + $"failed (duplicate may exist): {ex.Message}"
            );
        }
    }

    public string[] GetFilesInDirectory(string directoryPath) =>
        _cache.GetFilesInDirectory(directoryPath);

    public string[] GetDirectoriesInDirectory(string directoryPath) =>
        _cache.GetDirectoriesInDirectory(directoryPath);

    public void CreateDirectory(string directoryPath) { }

    public void DeleteDirectory(string directoryPath) { }

    public void DeleteTemporaryFiles(string directoryPath) { }

    public DateTimeOffset GetLastModifiedTime(string path) => _cache.GetLastModifiedTime(path);

    public int GetFileSize(string path) => _cache.GetFileSize(path);

    public void SetLastModifiedTime(string path, DateTimeOffset time) =>
        throw new NotImplementedException();

    public string GetFullPath(string filename) => throw new NotImplementedException();

    public bool HasCloudFiles() => _cache.HasCloudFiles();

    public void ForgetFile(string path) => _cache.ForgetFile(path);

    public bool IsFilePersisted(string path) => _cache.IsFilePersisted(path);

    public void BeginSaveBatch()
    {
        lock (_batchLock)
        {
            _collectingBatch = true;
            _batchPendingFiles.Clear();
        }
    }

    public void EndSaveBatch()
    {
        List<(string path, byte[] bytes)> files;
        lock (_batchLock)
        {
            _collectingBatch = false;

            if (_batchPendingFiles.Count == 0)
                return;

            files = new List<(string path, byte[] bytes)>(_batchPendingFiles);
            _batchPendingFiles.Clear();
        }

        _writeQueue.Enqueue(() =>
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
                    request.files_to_upload.Add(CloudFileCache.CanonicalizePath(path));

                var result = _connection
                    .SendCloud<
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
                _connection
                    .SendCloud<
                        CCloud_CompleteAppUploadBatch_Request,
                        CCloud_CompleteAppUploadBatch_Response
                    >(
                        "CompleteAppUploadBatchBlocking",
                        new CCloud_CompleteAppUploadBatch_Request
                        {
                            appid = AppId,
                            batch_id = batchId,
                            batch_eresult = (uint)SteamKit2.EResult.OK,
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
                UploadFileAsync(path, bytes, batchId, timestamp).GetAwaiter().GetResult();
                return;
            }
            catch (InvalidOperationException ex)
                when (ex.Message.Contains("TooManyPending") && attempt < 2)
            {
                PatchHelper.Log(
                    $"[Cloud] Upload throttled for {CloudFileCache.CanonicalizePath(path)}, "
                        + $"retrying in {(attempt + 1) * 2}s..."
                );
                Thread.Sleep((attempt + 1) * 2000);
            }
            catch (Exception ex)
            {
                PatchHelper.Log(
                    $"[Cloud] Upload failed for {CloudFileCache.CanonicalizePath(path)}: {ex.Message}"
                );
                return;
            }
        }
    }

    private async Task UploadFileAsync(
        string path,
        byte[] bytes,
        ulong batchId,
        DateTimeOffset? timestamp = null
    )
    {
        path = CloudFileCache.CanonicalizePath(path);

        var fileHash = SHA1.HashData(bytes);
        var rawSize = (uint)bytes.Length;
        var (uploadBytes, compressed) = CloudCompression.Compress(bytes);

        if (compressed)
            PatchHelper.Log($"[Cloud] Compressed {path} ({rawSize} → {uploadBytes.Length} bytes)");
        else
            PatchHelper.Log($"[Cloud] Uploading {path} uncompressed ({rawSize} bytes)");

        var uploadTimestamp = timestamp.HasValue
            ? (ulong)timestamp.Value.ToUnixTimeSeconds()
            : (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var beginRequest = new CCloud_ClientBeginFileUpload_Request
        {
            appid = AppId,
            filename = path,
            file_size = (uint)uploadBytes.Length,
            raw_file_size = rawSize,
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
            beginResult = await _connection
                .SendCloud<
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

        bool uploadSucceeded = false;
        try
        {
            foreach (var block in beginResult.block_requests)
            {
                var scheme = block.use_https ? "https" : "http";
                var url = $"{scheme}://{block.url_host}{block.url_path}";

                var method = block.http_method == 2 ? HttpMethod.Post : HttpMethod.Put;
                var request = new HttpRequestMessage(method, url);

                byte[] bodyData =
                    block.explicit_body_data?.Length > 0
                        ? block.explicit_body_data
                        : uploadBytes[
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

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var httpResponse = await _http.SendAsync(request, cts.Token).ConfigureAwait(false);
                httpResponse.EnsureSuccessStatusCode();
            }

            uploadSucceeded = true;
        }
        finally
        {
            try
            {
                var commitResult = await _connection
                    .SendCloud<
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

        PatchHelper.Log($"[Cloud] Wrote {bytes.Length} bytes to {path} (compressed={compressed})");
    }
}
