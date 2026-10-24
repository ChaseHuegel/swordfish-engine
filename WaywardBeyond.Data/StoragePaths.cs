using System.Collections.Generic;
using System.IO;
using Swordfish.Library.IO;
using WaywardBeyond.Config;

namespace WaywardBeyond.Data;

/// <summary>
/// Resolves the save-data file layout under the configured data root. The client profile database and
/// the per-level server databases share the root.
/// </summary>
public sealed class StoragePaths(StorageConfig config)
{
    private const string PROFILE_DATABASE_NAME = "profile.db";
    private const string LEVEL_DATABASE_NAME = "level.db";

    private PathInfo DataRoot => config.SaveRoot.Get();

    /// <summary>Gets the path to the client profile database.</summary>
    public PathInfo GetProfileDatabase() => DataRoot.At(PROFILE_DATABASE_NAME);

    /// <summary>Gets the directory holding a level's server database.</summary>
    public PathInfo GetLevelDirectory(string levelGuid) => DataRoot.At(levelGuid);

    /// <summary>Gets the path to a level's server database.</summary>
    public PathInfo GetLevelDatabase(string levelGuid) => GetLevelDirectory(levelGuid).At(LEVEL_DATABASE_NAME);

    /// <summary>Whether a save database exists for the provided level.</summary>
    public bool LevelExists(string levelGuid)
    {
        PathInfo levelDatabasePath = GetLevelDatabase(levelGuid);
        return levelDatabasePath.IsFile() && levelDatabasePath.FileExists();
    }

    /// <summary>Gets the guid of every level that has a save database under the data root.</summary>
    public string[] GetLevelGuids()
    {
        if (!DataRoot.IsDirectory() || !DataRoot.DirectoryExists())
        {
            return [];
        }

        var guids = new List<string>();
        foreach (string directory in Directory.EnumerateDirectories(DataRoot))
        {
            string guid = Path.GetFileName(directory);
            if (File.Exists(Path.Combine(directory, LEVEL_DATABASE_NAME)))
            {
                guids.Add(guid);
            }
        }

        return guids.ToArray();
    }
}