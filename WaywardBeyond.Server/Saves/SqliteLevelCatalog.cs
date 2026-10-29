using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Data.Migrations;
using WaywardBeyond.Server.Generation;

namespace WaywardBeyond.Server.Saves;

/// <summary>
/// SQLite-backed level catalog. Each level is a directory under the data root holding a <c>level.db</c>
/// (level metadata, voxel entities, character locations). The catalog owns creation, listing, and
/// deletion; the live world owns its store through <see cref="LevelSaveService"/>.
/// </summary>
public sealed class SqliteLevelCatalog(
    in ILogger<SqliteLevelCatalog> logger,
    in StoragePaths paths,
    IBrickRegistry brickRegistry,
    ILoggerFactory loggerFactory,
    in Migrator migrator,
    in GameplayConfig gameplayConfig,
    in LevelGeneratorConfig levelGeneratorConfig,
    in SeedPhraseService seedPhraseService
) : ILevelCatalog
{
    //  The game version stamped on every created level.
    private static readonly Data.Version _gameVersion = new(_DataVersion: DataVersion.CURRENT, _Name: "Wayward Beyond", _Environment: "Development");

    private readonly ILogger _logger = logger;
    private readonly StoragePaths _paths = paths;
    private readonly GameplayConfig _gameplayConfig = gameplayConfig;
    private readonly LevelGeneratorConfig _levelGeneratorConfig = levelGeneratorConfig;
    private readonly Migrator _migrator = migrator;
    private readonly SeedPhraseService _seedPhraseService = seedPhraseService;

    /// <inheritdoc/>
    public bool Create(string name, string seed, GameMode gameMode, out string levelGuid)
    {
        levelGuid = string.Empty;

        long seedValue = string.IsNullOrWhiteSpace(seed)
            ? _seedPhraseService.ResolveSeed(_seedPhraseService.SeedToPhrase(Random.Shared.NextInt64()))
            : _seedPhraseService.ResolveSeed(seed);
        Guid guid = Guid.NewGuid();
        string guidText = guid.ToString();
        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Vector3 spawnPosition = _gameplayConfig.DefaultSpawnPosition;
        var level = new Level(
            _gameVersion,
            seedValue,
            _Playtime: new Playtime { LastPlayedMs = nowUtcMs },
            _SpawnX: (int)spawnPosition.X,
            _SpawnY: (int)spawnPosition.Y,
            _SpawnZ: (int)spawnPosition.Z,
            gameMode,
            guidText,
            name
        );

        VoxelEntityData[] entities = new LevelGenerator(seedValue, brickRegistry, _levelGeneratorConfig).Generate();
        string directory = _paths.GetLevelDirectory(guidText);

        try
        {
            Directory.CreateDirectory(directory);

            using ILevelStorage store = new SqliteLevelStorage(loggerFactory.CreateLogger<SqliteLevelStorage>(), _paths, guidText, _migrator);
            Result result = store.Save(level, entities, []);
            if (!result.Success)
            {
                throw new Exception(result.Message);
            }
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

    /// <inheritdoc/>
    public Level[] ListLevels()
    {
        var levels = new List<Level>();
        foreach (string levelGuid in _paths.GetLevelGuids())
        {
            try
            {
                using ILevelStorage store = new SqliteLevelStorage(loggerFactory.CreateLogger<SqliteLevelStorage>(), _paths, levelGuid, _migrator);
                Result<Level> levelResult = store.GetLevel();
                if (!levelResult.Success)
                {
                    _logger.LogWarning("Skipping level \"{level}\" in the save list: {message}", levelGuid, levelResult.Message);
                    continue;
                }

                Level level = levelResult.Value;
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

    /// <inheritdoc/>
    public bool Delete(string levelGuid)
    {
        if (string.IsNullOrEmpty(levelGuid))
        {
            return false;
        }

        string directory = _paths.GetLevelDirectory(levelGuid);

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

    /// <inheritdoc/>
    public bool Exists(string levelGuid)
    {
        return !string.IsNullOrEmpty(levelGuid) && _paths.LevelExists(levelGuid);
    }

    /// <inheritdoc/>
    public ILevelStorage? Open(string levelGuid)
    {
        if (!Exists(levelGuid))
        {
            return null;
        }

        return new SqliteLevelStorage(loggerFactory.CreateLogger<SqliteLevelStorage>(), _paths, levelGuid, _migrator);
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
}
