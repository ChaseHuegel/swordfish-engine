using System;
using System.IO;
using System.IO.Compression;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.IO;

internal static class Zip
{
    public static Result<byte[]> Compress(params NamedStream[] streams)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create);

            foreach (NamedStream stream in streams)
            {
                ZipArchiveEntry entry = archive.CreateEntry(stream.Name);
                using var entryStream = entry.Open();
                stream.Value.CopyTo(entryStream);
            }

            return Result<byte[]>.FromSuccess(memoryStream.ToArray());
        }
        catch (Exception ex)
        {
            return new Result<byte[]>(success: false, null!, "Caught an exception during compression", ex);
        }
    }
}