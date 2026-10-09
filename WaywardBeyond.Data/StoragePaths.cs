using System.IO;
using WaywardBeyond.Config;

namespace WaywardBeyond.Data;

/// <summary>
/// Resolves the save-data file layout under the configured data root. The client profile database and
/// the per-level server databases share the root.
/// </summary>
public sealed class StoragePaths(in StorageSettings settings)
{
    private const string PROFILE_DATABASE_NAME = "profile.db";
    private const string LEVEL_DATABASE_NAME = "level.db";

    public string DataRoot { get; } = Path.GetFullPath(settings.DataRoot.Get());

    public string ProfileDatabasePath => Path.Combine(DataRoot, PROFILE_DATABASE_NAME);

    public string LevelDirectory(string levelGuid) => Path.Combine(DataRoot, levelGuid);

    public string LevelDatabasePath(string levelGuid) => Path.Combine(LevelDirectory(levelGuid), LEVEL_DATABASE_NAME);

    public bool LevelExists(string levelGuid)
    {
        return File.Exists(LevelDatabasePath(levelGuid));
    }

    public string[] ListLevelGuids()
    {
        if (!Directory.Exists(DataRoot))
        {
            return [];
        }

        var guids = new System.Collections.Generic.List<string>();
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
