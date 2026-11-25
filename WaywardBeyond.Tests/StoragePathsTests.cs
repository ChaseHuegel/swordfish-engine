using System.IO;
using Swordfish.Library.IO;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using Xunit;

namespace WaywardBeyond.Tests;

public class StoragePathsTests
{
    [Fact]
    public void RelativeSaveRoot_ResolvesUnderAppDataRoot()
    {
        string userRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var storageConfig = new StorageConfig();
        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(userRoot));
        var storagePaths = new StoragePaths(storageConfig, appPaths);

        Assert.Equal(Path.Combine(userRoot, "saves/", "profile.db"), storagePaths.GetProfileDatabase().Value);
        Assert.Equal(Path.Combine(userRoot, "saves/", "level-1", "level.db"), storagePaths.GetLevelDatabase("level-1").Value);
        Assert.Equal(Path.Combine(userRoot, "blueprints/"), storagePaths.GetBlueprintsDirectory().Value);
    }

    [Fact]
    public void RootedSaveRoot_PreservesExplicitPathAndEnumeratesLevels()
    {
        string explicitRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string levelDir = Path.Combine(explicitRoot, "alpha");
        Directory.CreateDirectory(levelDir);
        File.WriteAllText(Path.Combine(levelDir, "level.db"), string.Empty);

        try
        {
            var storageConfig = new StorageConfig();
            storageConfig.SaveRoot.Set(new PathInfo(explicitRoot));
            AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(Path.Combine(Path.GetTempPath(), "ignored-user-root")));
            var storagePaths = new StoragePaths(storageConfig, appPaths);

            Assert.Equal(Path.Combine(explicitRoot, "profile.db"), storagePaths.GetProfileDatabase().Value);
            Assert.True(storagePaths.LevelExists("alpha"));
            Assert.Equal(["alpha"], storagePaths.GetLevelGuids());
        }
        finally
        {
            Directory.Delete(explicitRoot, recursive: true);
        }
    }
}
