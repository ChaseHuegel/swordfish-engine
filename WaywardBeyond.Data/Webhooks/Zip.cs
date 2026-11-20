using System;
using System.IO;
using System.IO.Compression;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Webhooks;

/// <summary>
///     Compresses named streams into an in-memory ZIP archive.
/// </summary>
public static class Zip
{
    /// <summary>
    ///     Compresses all provided streams into a ZIP byte array.
    /// </summary>
    public static Result<byte[]> Compress(params NamedStream[] streams)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (NamedStream stream in streams)
                {
                    if (stream.Value.CanSeek)
                    {
                        stream.Value.Position = 0;
                    }

                    ZipArchiveEntry entry = archive.CreateEntry(stream.Name, CompressionLevel.Optimal);
                    using Stream entryStream = entry.Open();
                    stream.Value.CopyTo(entryStream);
                }
            }

            return Result<byte[]>.FromSuccess(memoryStream.ToArray());
        }
        catch (Exception ex)
        {
            return new Result<byte[]>(success: false, null!, "Caught an exception during compression", ex);
        }
    }
}
