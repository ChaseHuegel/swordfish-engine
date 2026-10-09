using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay;

namespace WaywardBeyond.Server.Saves;

/// <summary>
/// SQLite-backed level catalog. Each level is a directory under the data root holding a <c>level.db</c>
/// (level metadata, voxel entities, character locations). The catalog owns creation, listing, and
/// deletion; the live world owns its store through <see cref="LevelSaveService"/>.
/// </summary>
public sealed class SqliteLevelCatalog : ILevelCatalog
{
    private static readonly WaywardBeyond.Data.Version _gameVersion = new(_DataVersion: SaveVersion.CurrentDataVersion, _Name: "Wayward Beyond", _Environment: "Development");

    private readonly ILogger _logger;
    private readonly StoragePaths _paths;
    private readonly IBrickIdMap _brickIdMap;

    public SqliteLevelCatalog(in ILogger<SqliteLevelCatalog> logger, in StoragePaths paths, IBrickIdMap brickIdMap)
    {
        _logger = logger;
        _paths = paths;
        _brickIdMap = brickIdMap;
    }

    public bool Create(string name, string seed, GameMode gameMode, out string levelGuid)
    {
        levelGuid = string.Empty;

        int seedValue = LevelGenerator.HashSeed(seed);
        Guid guid = Guid.NewGuid();
        string guidText = guid.ToString();
        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var level = new Level(
            _gameVersion,
            seedValue,
            nowUtcMs,
            _AgeMs: 0,
            _SpawnX: 0,
            _SpawnY: 1,
            _SpawnZ: 5,
            gameMode,
            guidText,
            name
        );

        GeneratedVoxelEntity[] entities = new LevelGenerator(seedValue, _brickIdMap).Generate();
        string directory = _paths.LevelDirectory(guidText);

        try
        {
            Directory.CreateDirectory(directory);

            using ILevelStore store = new SqliteLevelStore(_paths, guidText);
            var records = new List<LevelEntityRecord>(entities.Length);
            foreach (GeneratedVoxelEntity entity in entities)
            {
                VoxelEntityData data = ToVoxelEntityData(entity, _brickIdMap);
                records.Add(new LevelEntityRecord(entity.Uuid.ToValue(), data.Serialize()));
            }

            store.WriteSave(level.Serialize(), records, []);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist generated level \"{name}\".", name);
            TryDeleteDirectory(directory);
            return false;
        }

        levelGuid = guidText;
        _logger.LogInformation("Generated and persisted level \"{name}\" ({guid}) with {entities} structures.", name, levelGuid, entities.Length);
        return true;
    }

    public Level[] ListLevels()
    {
        var levels = new List<Level>();
        foreach (string levelGuid in _paths.ListLevelGuids())
        {
            try
            {
                using ILevelStore store = new SqliteLevelStore(_paths, levelGuid);
                byte[]? data = store.ReadLevel();
                if (data == null || data.Length == 0)
                {
                    continue;
                }

                Level level = Level.Deserialize(data);
                if (!GameSaveMigrations.Migrator.IsSupported(level.Version.DataVersion))
                {
                    _logger.LogWarning(
                        "Skipping level \"{level}\" with newer data version {version} in the save list.",
                        levelGuid, level.Version.DataVersion);
                    continue;
                }

                if (!string.IsNullOrEmpty(level.Guid))
                {
                    levels.Add(level);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read level metadata for \"{level}\".", levelGuid);
            }
        }

        return levels.ToArray();
    }

    public bool Delete(string levelGuid)
    {
        if (string.IsNullOrEmpty(levelGuid))
        {
            return false;
        }

        string directory = _paths.LevelDirectory(levelGuid);

        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete level \"{level}\".", levelGuid);
            return false;
        }
    }

    public bool Exists(string levelGuid)
    {
        return !string.IsNullOrEmpty(levelGuid) && _paths.LevelExists(levelGuid);
    }

    public ILevelStore? Open(string levelGuid)
    {
        if (!Exists(levelGuid))
        {
            return null;
        }

        return new SqliteLevelStore(_paths, levelGuid);
    }

    private void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up level directory \"{directory}\".", directory);
        }
    }

    private static VoxelEntityData ToVoxelEntityData(in GeneratedVoxelEntity entity, IBrickIdMap brickIdMap)
    {
        return VoxelEntityDataCodec.EncodeToPalette(new VoxelEntityData(
            entity.Uuid.ToValue(),
            entity.Position.X,
            entity.Position.Y,
            entity.Position.Z,
            entity.Orientation.X,
            entity.Orientation.Y,
            entity.Orientation.Z,
            entity.Orientation.W,
            _ScaleX: 1,
            _ScaleY: 1,
            _ScaleZ: 1,
            entity.Chunks,
            _BrickPalette: null
        ), brickIdMap);
    }
}
