using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Data;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Round-trip coverage for the SQLite save stores: the per-level database, the catalog, the character
/// store, and save-listing metadata.
/// </summary>
public class SqliteStorageTests
{
    [Fact]
    public void LevelStoreRoundTripsMetadataEntitiesAndLocations()
    {
        using TempDataRoot temp = new();
        ILevelCatalog catalog = temp.Catalog;

        Assert.True(catalog.Create("Round Trip", seed: "7", GameMode.Creative, out string levelGuid));

        using ILevelStore store = catalog.Open(levelGuid)!;
        Assert.NotNull(store.ReadLevel());
        Assert.NotEmpty(store.ReadEntities());

        store.WriteLocation(42, [1, 2, 3]);
        Assert.Equal([1, 2, 3], store.ReadLocation(42));
        Assert.Null(store.ReadLocation(7));
    }

    [Fact]
    public void WriteSaveReplacesEntitiesAndPreservesAbsentLocations()
    {
        using TempDataRoot temp = new();
        ILevelCatalog catalog = temp.Catalog;

        Assert.True(catalog.Create("Snapshot", seed: "9", GameMode.Creative, out string levelGuid));

        using ILevelStore store = catalog.Open(levelGuid)!;
        byte[] levelData = store.ReadLevel()!;

        store.WriteLocation(1, [1]);
        store.WriteLocation(2, [2]);

        //  A save without character 1 must not drop its last location.
        store.WriteSave(levelData, [], [new LevelLocationRecord(2, [20])]);

        Assert.Empty(store.ReadEntities());
        Assert.Equal([1], store.ReadLocation(1));
        Assert.Equal([20], store.ReadLocation(2));
    }

    [Fact]
    public void CatalogListsAndDeletesLevels()
    {
        using TempDataRoot temp = new();
        ILevelCatalog catalog = temp.Catalog;

        Assert.True(catalog.Create("Keep", seed: "1", GameMode.Creative, out string keepGuid));
        Assert.True(catalog.Create("Delete", seed: "2", GameMode.Creative, out string deleteGuid));

        Assert.True(catalog.Exists(keepGuid));
        Assert.True(catalog.Exists(deleteGuid));
        Assert.Contains(catalog.ListLevels(), level => level.Guid == keepGuid);

        Assert.True(catalog.Delete(deleteGuid));
        Assert.False(catalog.Exists(deleteGuid));
        Assert.Null(catalog.Open(deleteGuid));
        Assert.True(catalog.Exists(keepGuid));
    }

    [Fact]
    public void CharacterStoreRoundTripsAndDeletes()
    {
        using TempDataRoot temp = new();
        var storage = new SqliteCharacterStorage(temp.Paths);

        Character character = CreateCharacter(id: 11, name: "Pilot");
        Assert.True(storage.SaveCharacter(character));

        Result<Character> loaded = storage.GetCharacter(11);
        Assert.True(loaded.Success);
        Assert.Equal("Pilot", loaded.Value.Name);

        character = character with { Name = "Renamed" };
        Assert.True(storage.SaveCharacter(character));
        Assert.Equal("Renamed", storage.GetCharacter(11).Value.Name);
        Assert.Single(storage.GetAllCharacters());

        Assert.True(storage.DeleteCharacter(11));
        Assert.False(storage.GetCharacter(11));
    }

    [Fact]
    public void SaveMetaStoreRoundTripsAndDeletes()
    {
        using TempDataRoot temp = new();
        var storage = new SqliteSaveMetaStorage(temp.Paths);

        Assert.False(storage.Get("level-a").Success);

        var meta = new SaveMeta(123, 456);
        Assert.True(storage.Save("level-a", meta));

        Result<SaveMeta> loaded = storage.Get("level-a");
        Assert.True(loaded.Success);
        Assert.Equal(123, loaded.Value.LastPlayedMs);
        Assert.Equal(456, loaded.Value.AgeMs);

        Assert.Single(storage.GetAll());
        Assert.True(storage.Delete("level-a"));
        Assert.Empty(storage.GetAll());
    }

    private static Character CreateCharacter(ulong id, string name)
    {
        return new Character(
            new WaywardBeyond.Shared.Data.Version(SaveVersion.CurrentDataVersion, "test", "test"),
            id,
            0,
            0,
            name,
            1,
            1,
            1,
            1,
            1,
            1,
            "wb:m_human",
            0,
            GameMode.Creative,
            null,
            null
        );
    }

    /// <summary>A throwaway data root with a real catalog, removed on dispose.</summary>
    private sealed class TempDataRoot : IDisposable
    {
        private readonly string _root;

        public StoragePaths Paths { get; }

        public ILevelCatalog Catalog { get; }

        public TempDataRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "wb_storage_" + Guid.NewGuid().ToString("N"));
            var settings = new StorageSettings();
            settings.DataRoot.Set(_root);
            Paths = new StoragePaths(settings);
            Catalog = new SqliteLevelCatalog(NullLogger<SqliteLevelCatalog>.Instance, Paths, TestBricks.Map);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
            }
            catch
            {
                //  Best-effort teardown.
            }
        }
    }
}
