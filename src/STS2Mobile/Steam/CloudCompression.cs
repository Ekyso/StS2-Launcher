using System.IO;
using System.IO.Compression;

namespace STS2Mobile.Steam;

// Compresses and decompresses cloud save files using ZIP format, matching the
// format Steam serves on download. Static utility — no connection dependency.
public static class CloudCompression
{
    // Creates a single-entry ZIP archive from raw bytes. Returns the raw bytes
    // unchanged if compression doesn't reduce size (small files with ZIP overhead).
    public static (byte[] data, bool compressed) Compress(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("data", CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            entryStream.Write(raw, 0, raw.Length);
        }

        var zipped = ms.ToArray();
        if (zipped.Length >= raw.Length)
            return (raw, false);

        return (zipped, true);
    }

    public static byte[] Decompress(byte[] zipData)
    {
        using var archive = new ZipArchive(new MemoryStream(zipData), ZipArchiveMode.Read);
        var entry = archive.Entries[0];
        using var stream = entry.Open();
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
