using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.IO;

public static class PathInfoExtensions
{
    public static Stream Open(this PathInfo path)
    {
        switch (path.Scheme)
        {
            case "manifest":
                var assembly = Assembly.GetCallingAssembly();
                string assemblyName = assembly.GetName().Name;

                const string manifestRoot = ".Manifest.";
                var builder = new StringBuilder(
                    assemblyName.Length + manifestRoot.Length + path.Value.Length
                );

                builder.Append(assemblyName);
                builder.Append(manifestRoot);
                builder.Append(path.Value.Substitute('.', '/', '\\'));

                return assembly.GetManifestResourceStream(builder.ToString());
            
            case "zip":
                //  Walk back from the end until finding the archive file.
                PathInfo zipPath = path;
                while (!zipPath.FileExists())
                {
                    zipPath = zipPath.GetDirectory();
                }
                
                int zipPathLength = zipPath.Value.Length;
                if (zipPathLength == path.Value.Length)
                {
                    throw new FileNotFoundException("Zip path does point include a file.", path.Value);
                }

                ZipArchive zip = ZipFile.OpenRead(zipPath.Value);
                string zipEntryPath = path.Value[(zipPathLength + 1)..];
                ZipArchiveEntry entry = zip.GetEntry(zipEntryPath);
                if (entry == null)
                {
                    throw new FileNotFoundException("Zip entry not found.", path.Value);
                }
                
                return new ZipArchiveEntryStream(zip, entry.Open());
        }

        return File.Open(path.Value, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public static byte[] ReadBytes(this PathInfo path)
    {
        using Stream stream = Open(path);
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    public static string ReadString(this PathInfo path)
    {
        using Stream stream = Open(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static void Write(this PathInfo path, Stream stream)
    {
        using Stream output = File.Open(path.Value, FileMode.OpenOrCreate, FileAccess.Write);
        stream.CopyTo(output);
    }

    public static PathInfo[] GetFiles(this PathInfo path, SearchOption searchOption)
    {
        return GetFiles(path, "*", searchOption);
    }

    public static PathInfo[] GetFiles(this PathInfo path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        string dir = path.GetDirectory().Value;
        if (!Directory.Exists(dir))
        {
            return [];
        }
            
        return Directory.GetFiles(dir, searchPattern, searchOption)
            .Select(str => new PathInfo(str))
            .ToArray();
    }
    
    public static PathInfo[] GetFolders(this PathInfo path, SearchOption searchOption)
    {
        return GetFolders(path, "*", searchOption);
    }
    
    public static PathInfo[] GetFolders(this PathInfo path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        string dir = path.GetDirectory().Value;
        if (!Directory.Exists(dir))
        {
            return [];
        }
            
        return Directory.GetDirectories(dir, searchPattern, searchOption)
            .Select(str => new PathInfo(str))
            .ToArray();
    }
    
    public static bool TryOpenInDefaultApp(this PathInfo pathInfo)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = pathInfo.Value,
            UseShellExecute = true,
            Verb = "open",
        };

        return Safe.Invoke(
            () => Process.Start(processStartInfo)
        );
    }

    public static bool HasExtension(this PathInfo path, string extension)
    {
        return path.GetExtension().Equals(extension, StringComparison.OrdinalIgnoreCase);
    }
}