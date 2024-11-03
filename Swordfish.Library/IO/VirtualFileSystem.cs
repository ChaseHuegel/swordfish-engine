using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Swordfish.Library.Collections.Filtering;
using Swordfish.Library.Util;

namespace Swordfish.Library.IO;

public class VirtualFileSystem
{
    private readonly Dictionary<PathInfo, PathInfo> _files = [];
    private readonly HashSet<PathInfo> _folders = [];
    
    public Result Mount(PathInfo path)
    {
        if (path.FileExists())
        {
            try
            {
                ReadArchive(path);
                return Result.FromSuccess();
            }
            catch (Exception ex)
            {
                return Result.FromFailure($"Failed to mount an archive: {ex}");
            }
        }

        if (!path.DirectoryExists())
        {
            return Result.FromFailure("No directory or archive was found at the mount path.");
        }
        
        try
        {
            ReadDirectory(path);
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return Result.FromFailure($"Failed to mount a directory: {ex}");
        }
    }

    public bool FileExists(PathInfo path)
    {
        return _files.ContainsKey(path.Normalize());
    }
    
    public bool DirectoryExists(PathInfo path)
    {
        return _folders.Contains(path.Normalize());
    }

    public bool TryGetFile(PathInfo path, out PathInfo file)
    {
        return _files.TryGetValue(path.Normalize(), out file);
    }

    public PathInfo[] GetFiles(PathInfo path, SearchOption searchOption)
    {
        path = path.Normalize();
        List<PathInfo> files = [];
        
        foreach ((PathInfo virtualPath, PathInfo absolutePath) in _files)
        {
            if (!virtualPath.Value.StartsWith(path.Value))
            {
                continue;
            }

            if (searchOption != SearchOption.AllDirectories && path.GetDirectory() != virtualPath.GetDirectory())
            {
                continue;
            }

            files.Add(absolutePath);
        }
        
        return files.ToArray();
    }

    private void ReadDirectory(PathInfo path)
    {
        string[] files = Directory.GetFiles(path.Value, "*", SearchOption.AllDirectories).ToArray();
        for (int i = 0; i < files.Length; i++)
        {
            var relativePath = new PathInfo(path.Scheme, Path.GetRelativePath(path.Value, files[i])).Normalize();

            _files[relativePath] = path.At(relativePath).Normalize();
            
            string folder = relativePath.GetDirectoryName() ?? string.Empty;
            _folders.Add(new PathInfo(folder + "/"));
        }
    }
    
    private void ReadArchive(PathInfo path)
    {
        using ZipArchive zip = ZipFile.OpenRead(path.Value);
        for (int i = 0; i < zip.Entries.Count; i++)
        {
            var entryPath = new PathInfo(zip.Entries[i].FullName);
            if (!entryPath.IsFile())
            {
                continue;
            }
            
            var relativePath = entryPath.Normalize();
            
            _files[relativePath] = new PathInfo("zip", path.Value).At(relativePath).Normalize();
            
            string folder = relativePath.GetDirectoryName() ?? string.Empty;
            _folders.Add(new PathInfo(folder + "/"));
        }
    }
}