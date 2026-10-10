using System.Collections.Generic;
using System.IO;
using Swordfish.Library.IO;
using WaywardBeyond.Config;

namespace WaywardBeyond.Data;

/// <summary>
/// Resolves the save-data file layout under the configured data root. The client profile database and
/// the per-level server databases share the root.
/// </summary>
public sealed class StoragePaths(StorageSettings settings)
{
    private const string PROFILE_DATABASE_NAME = "profile.db";
    private const string LEVEL_DATABASE_NAME = "level.db";

    private PathInfo DataRoot => settings.SaveRoot.Get();

    public PathInfo ProfileDatabasePath => DataRoot.At(PROFILE_DATABASE_NAME);

    public PathInfo LevelDirectory(string levelGuid) => DataRoot.At(levelGuid);

    public PathInfo LevelDatabasePath(string levelGuid) => LevelDirectory(levelGuid).At(LEVEL_DATABASE_NAME);

    public bool LevelExists(string levelGuid)
    {
        PathInfo levelDatabasePath = LevelDatabasePath(levelGuid);
        return levelDatabasePath.IsFile() && levelDatabasePath.FileExists();
    }

    public string[] ListLevelGuids()
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
