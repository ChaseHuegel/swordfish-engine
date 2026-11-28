using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Migrations;
using WaywardBeyond.Data.Sqlite;

namespace WaywardBeyond.Data.Levels;

/// <summary>SQLite-backed per-level save store.</summary>
public sealed class SqliteLevelStorage : ILevelStorage
{
    private const string CREATE_LEVEL_TABLE_COMMAND = "CREATE TABLE IF NOT EXISTS level (guid TEXT PRIMARY KEY, data BLOB NOT NULL);";
    private const string CREATE_ENTITIES_TABLE_COMMAND = "CREATE TABLE IF NOT EXISTS entities (uuid TEXT PRIMARY KEY, data BLOB NOT NULL);";
    private const string CREATE_CHARACTERS_TABLE_COMMAND = "CREATE TABLE IF NOT EXISTS character_locations (character_uuid TEXT PRIMARY KEY, data BLOB NOT NULL);";
    private const string CREATE_BRICK_ENTITIES_TABLE_COMMAND = """
        CREATE TABLE IF NOT EXISTS brick_entities (
            voxel_entity_uuid TEXT NOT NULL,
            voxel_x INTEGER NOT NULL,
            voxel_y INTEGER NOT NULL,
            voxel_z INTEGER NOT NULL,
            data BLOB NOT NULL,
            PRIMARY KEY (voxel_entity_uuid, voxel_x, voxel_y, voxel_z)
        );
        """;

    private readonly ILogger _logger;
    private readonly string _levelGuid;
    private readonly Migrator _migrator;
    private readonly SqliteDatabase _database;
    private readonly SqliteTable _levelTable;
    private readonly SqliteTable _entitiesTable;
    private readonly SqliteTable _charactersTable;
    private readonly SqliteTable _brickEntitiesTable;
    private readonly Lock _writeLock = new();
    
    private bool _disposed;

    public SqliteLevelStorage(in ILogger<SqliteLevelStorage> logger, in StoragePaths paths, string levelGuid, in Migrator migrator)
    {
        _logger = logger;
        _levelGuid = levelGuid;
        _migrator = migrator;
        
        PathInfo dbPath = paths.GetLevelDatabase(levelGuid);
        _database = new SqliteDatabase(dbPath);
        _levelTable = new SqliteTable(CREATE_LEVEL_TABLE_COMMAND);
        _entitiesTable = new SqliteTable(CREATE_ENTITIES_TABLE_COMMAND);
        _charactersTable = new SqliteTable(CREATE_CHARACTERS_TABLE_COMMAND);
        _brickEntitiesTable = new SqliteTable(CREATE_BRICK_ENTITIES_TABLE_COMMAND);
    }

    /// <inheritdoc/>
    public Result<Level> GetLevel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            using SqliteConnection connection = _database.Open();
            using SqliteCommand command = _levelTable.CreateCommand(connection);
            command.CommandText = "SELECT data FROM level LIMIT 1;";

            if (command.ExecuteScalar() is not byte[] data || data.Length == 0)
            {
                return Result<Level>.FromFailure("Level not found.");
            }

            return DeserializeLevel(data);
        }
        catch (Exception ex)
        {
            return Result<Level>.FromFailure(ex);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<VoxelEntityData> GetVoxelEntities(uint dataVersion)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var entities = new List<VoxelEntityData>();

        try
        {
            using SqliteConnection connection = _database.Open();
            using SqliteCommand command = _entitiesTable.CreateCommand(connection);
            command.CommandText = "SELECT data FROM entities;";

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                byte[] data = (byte[])reader["data"];
                Result<VoxelEntityData> result = DeserializeVoxelEntity(data, dataVersion);
                if (result.Success)
                {
                    entities.Add(result.Value);
                }
                else
                {
                    _logger.LogWarning("Skipping an unreadable level entity: {message}", result.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list level entities.");
        }

        return entities;
    }

    /// <inheritdoc/>
    public IReadOnlyList<BrickEntityData> GetBrickEntities()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var brickEntities = new List<BrickEntityData>();

        try
        {
            using SqliteConnection connection = _database.Open();
            using SqliteCommand command = _brickEntitiesTable.CreateCommand(connection);
            command.CommandText = "SELECT data FROM brick_entities;";

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                byte[] data = (byte[])reader["data"];
                try
                {
                    brickEntities.Add(BrickEntityData.Deserialize(data));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Skipping an unreadable brick-entity record.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list brick-entities.");
        }

        return brickEntities;
    }

    /// <inheritdoc/>
    public Result<CharacterEntityData> GetCharacterEntity(ulong uuid)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            using SqliteConnection connection = _database.Open();
            using SqliteCommand command = _charactersTable.CreateCommand(connection);
            command.CommandText = "SELECT data FROM character_locations WHERE character_uuid = $characterUuid LIMIT 1;";
            command.Parameters.AddWithValue("$characterUuid", SqliteDatabase.EncodeKey(uuid));

            if (command.ExecuteScalar() is not byte[] data || data.Length == 0)
            {
                return Result<CharacterEntityData>.FromFailure("Location not found.");
            }

            return Result<CharacterEntityData>.FromSuccess(CharacterEntityData.Deserialize(data));
        }
        catch (Exception ex)
        {
            return Result<CharacterEntityData>.FromFailure(ex);
        }
    }

    /// <inheritdoc/>
    public Result Save(Level level, IReadOnlyList<VoxelEntityData> voxelEntities, IReadOnlyList<CharacterEntityData> characterEntities)
    {
        return SaveCore(level, voxelEntities, characterEntities, brickEntities: null);
    }

    /// <inheritdoc/>
    public Result Save(
        Level level,
        IReadOnlyList<VoxelEntityData> voxelEntities,
        IReadOnlyList<CharacterEntityData> characterEntities,
        IReadOnlyList<BrickEntityData> brickEntities
    )
    {
        return SaveCore(level, voxelEntities, characterEntities, brickEntities);
    }

    /// <inheritdoc/>
    public Result DeleteBrickEntities(IReadOnlyList<BrickEntityKey> keys)
    {
        using Lock.Scope _ = _writeLock.EnterScope();
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (keys == null || keys.Count == 0)
        {
            return Result.FromSuccess();
        }

        try
        {
            using SqliteDatabase.Transaction transaction = _database.BeginTransaction(deferred: false);
            using SqliteCommand delete = _brickEntitiesTable.CreateCommand(transaction.Connection);
            delete.CommandText = """
                DELETE FROM brick_entities
                WHERE voxel_entity_uuid = $uuid AND voxel_x = $x AND voxel_y = $y AND voxel_z = $z;
                """;
            delete.Parameters.Add("$uuid", SqliteType.Text);
            delete.Parameters.Add("$x", SqliteType.Integer);
            delete.Parameters.Add("$y", SqliteType.Integer);
            delete.Parameters.Add("$z", SqliteType.Integer);

            for (var i = 0; i < keys.Count; i++)
            {
                BrickEntityKey key = keys[i];
                delete.Parameters["$uuid"].Value = SqliteDatabase.EncodeKey(key.VoxelEntityUuid);
                delete.Parameters["$x"].Value = key.VoxelX;
                delete.Parameters["$y"].Value = key.VoxelY;
                delete.Parameters["$z"].Value = key.VoxelZ;
                delete.ExecuteNonQuery();
            }

            transaction.Value.Commit();
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return new Result(false, $"Failed to delete orphaned brick-entities: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public Result SaveStructure(VoxelEntityData voxelEntity, IReadOnlyList<BrickEntityData> brickEntities)
    {
        using Lock.Scope _ = _writeLock.EnterScope();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            using SqliteDatabase.Transaction transaction = _database.BeginTransaction(deferred: false);
            string encodedUuid = SqliteDatabase.EncodeKey(voxelEntity.Uuid);

            using (SqliteCommand upsertEntity = _entitiesTable.CreateCommand(transaction.Connection))
            {
                upsertEntity.CommandText = """
                    INSERT INTO entities (uuid, data) VALUES ($uuid, $data)
                    ON CONFLICT(uuid) DO UPDATE SET data = excluded.data;
                    """;
                upsertEntity.Parameters.AddWithValue("$uuid", encodedUuid);
                upsertEntity.Parameters.AddWithValue("$data", voxelEntity.Serialize());
                upsertEntity.ExecuteNonQuery();
            }

            using (SqliteCommand deleteBricks = _brickEntitiesTable.CreateCommand(transaction.Connection))
            {
                deleteBricks.CommandText = "DELETE FROM brick_entities WHERE voxel_entity_uuid = $uuid;";
                deleteBricks.Parameters.AddWithValue("$uuid", encodedUuid);
                deleteBricks.ExecuteNonQuery();
            }

            if (brickEntities != null && brickEntities.Count > 0)
            {
                using SqliteCommand insertBricks = _brickEntitiesTable.CreateCommand(transaction.Connection);
                insertBricks.CommandText = """
                    INSERT OR REPLACE INTO brick_entities (voxel_entity_uuid, voxel_x, voxel_y, voxel_z, data)
                    VALUES ($uuid, $x, $y, $z, $data);
                    """;
                insertBricks.Parameters.Add("$uuid", SqliteType.Text);
                insertBricks.Parameters.Add("$x", SqliteType.Integer);
                insertBricks.Parameters.Add("$y", SqliteType.Integer);
                insertBricks.Parameters.Add("$z", SqliteType.Integer);
                insertBricks.Parameters.Add("$data", SqliteType.Blob);

                for (var i = 0; i < brickEntities.Count; i++)
                {
                    BrickEntityData brick = brickEntities[i];
                    insertBricks.Parameters["$uuid"].Value = SqliteDatabase.EncodeKey(brick.VoxelEntityUuid);
                    insertBricks.Parameters["$x"].Value = brick.VoxelX;
                    insertBricks.Parameters["$y"].Value = brick.VoxelY;
                    insertBricks.Parameters["$z"].Value = brick.VoxelZ;
                    insertBricks.Parameters["$data"].Value = brick.Serialize();
                    insertBricks.ExecuteNonQuery();
                }
            }

            transaction.Value.Commit();
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return new Result(false, $"Failed to save structure {voxelEntity.Uuid}: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public Result DeleteStructure(ulong voxelEntityUuid)
    {
        using Lock.Scope _ = _writeLock.EnterScope();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            using SqliteDatabase.Transaction transaction = _database.BeginTransaction(deferred: false);
            string encodedUuid = SqliteDatabase.EncodeKey(voxelEntityUuid);

            using (SqliteCommand deleteBricks = _brickEntitiesTable.CreateCommand(transaction.Connection))
            {
                deleteBricks.CommandText = "DELETE FROM brick_entities WHERE voxel_entity_uuid = $uuid;";
                deleteBricks.Parameters.AddWithValue("$uuid", encodedUuid);
                deleteBricks.ExecuteNonQuery();
            }

            using (SqliteCommand deleteEntity = _entitiesTable.CreateCommand(transaction.Connection))
            {
                deleteEntity.CommandText = "DELETE FROM entities WHERE uuid = $uuid;";
                deleteEntity.Parameters.AddWithValue("$uuid", encodedUuid);
                deleteEntity.ExecuteNonQuery();
            }

            transaction.Value.Commit();
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return new Result(false, $"Failed to delete structure {voxelEntityUuid}: {ex.Message}", ex);
        }
    }

    private Result SaveCore(
        Level level,
        IReadOnlyList<VoxelEntityData> voxelEntities,
        IReadOnlyList<CharacterEntityData> characterEntities,
        IReadOnlyList<BrickEntityData>? brickEntities
    )
    {
        using Lock.Scope _ = _writeLock.EnterScope();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            using SqliteDatabase.Transaction transaction = _database.BeginTransaction(deferred: false);

            WriteLevel(transaction.Connection, level);

            using (SqliteCommand delete = _entitiesTable.CreateCommand(transaction.Connection))
            {
                delete.CommandText = "DELETE FROM entities;";
                delete.ExecuteNonQuery();
            }

            using (SqliteCommand insert = _entitiesTable.CreateCommand(transaction.Connection))
            {
                insert.CommandText = "INSERT INTO entities (uuid, data) VALUES ($uuid, $data);";
                insert.Parameters.Add("$uuid", SqliteType.Text);
                insert.Parameters.Add("$data", SqliteType.Blob);

                foreach (VoxelEntityData entity in voxelEntities)
                {
                    insert.Parameters["$uuid"].Value = SqliteDatabase.EncodeKey(entity.Uuid);
                    insert.Parameters["$data"].Value = entity.Serialize();
                    insert.ExecuteNonQuery();
                }
            }

            if (brickEntities != null)
            {
                using (SqliteCommand deleteBricks = _brickEntitiesTable.CreateCommand(transaction.Connection))
                {
                    deleteBricks.CommandText = "DELETE FROM brick_entities;";
                    deleteBricks.ExecuteNonQuery();
                }

                using (SqliteCommand insertBricks = _brickEntitiesTable.CreateCommand(transaction.Connection))
                {
                    insertBricks.CommandText = """
                        INSERT OR REPLACE INTO brick_entities (voxel_entity_uuid, voxel_x, voxel_y, voxel_z, data)
                        VALUES ($uuid, $x, $y, $z, $data);
                        """;
                    insertBricks.Parameters.Add("$uuid", SqliteType.Text);
                    insertBricks.Parameters.Add("$x", SqliteType.Integer);
                    insertBricks.Parameters.Add("$y", SqliteType.Integer);
                    insertBricks.Parameters.Add("$z", SqliteType.Integer);
                    insertBricks.Parameters.Add("$data", SqliteType.Blob);

                    for (var i = 0; i < brickEntities.Count; i++)
                    {
                        BrickEntityData brick = brickEntities[i];
                        insertBricks.Parameters["$uuid"].Value = SqliteDatabase.EncodeKey(brick.VoxelEntityUuid);
                        insertBricks.Parameters["$x"].Value = brick.VoxelX;
                        insertBricks.Parameters["$y"].Value = brick.VoxelY;
                        insertBricks.Parameters["$z"].Value = brick.VoxelZ;
                        insertBricks.Parameters["$data"].Value = brick.Serialize();
                        insertBricks.ExecuteNonQuery();
                    }
                }
            }

            foreach (CharacterEntityData characterEntity in characterEntities)
            {
                WriteCharacterEntity(transaction.Connection, characterEntity);
            }

            transaction.Value.Commit();
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return new Result(false, $"Failed to save level snapshot: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public Result Save(Level level)
    {
        using Lock.Scope _ = _writeLock.EnterScope();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            using SqliteConnection connection = _database.Open();
            WriteLevel(connection, level);
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return new Result(false, $"Failed to save level metadata: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public Result Save(CharacterEntityData characterEntity)
    {
        using Lock.Scope _ = _writeLock.EnterScope();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            using SqliteConnection connection = _database.Open();
            WriteCharacterEntity(connection, characterEntity);
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return new Result(false, $"Failed to save character location: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        using Lock.Scope _ = _writeLock.EnterScope();
        _disposed = true;
    }

    private Result<Level> DeserializeLevel(byte[] data)
    {
        try
        {
            Level level = Level.Deserialize(data);
            if (!_migrator.IsSupported<Level>(level.Version.DataVersion))
            {
                return Result<Level>.FromFailure($"Level uses data version {level.Version.DataVersion}, which is newer than the supported format version {DataVersion.CURRENT}.");
            }

            return Result<Level>.FromSuccess(_migrator.Migrate(level, level.Version.DataVersion));
        }
        catch (Exception ex)
        {
            return Result<Level>.FromFailure($"Failed to deserialize level: {ex.Message}");
        }
    }

    private Result<VoxelEntityData> DeserializeVoxelEntity(byte[] data, uint dataVersion)
    {
        try
        {
            VoxelEntityData entity = VoxelEntityData.Deserialize(data);
            if (!_migrator.IsSupported<VoxelEntityData>(dataVersion))
            {
                return Result<VoxelEntityData>.FromFailure($"Voxel entity uses data version {dataVersion}, which is newer than the supported format version {DataVersion.CURRENT}.");
            }

            return Result<VoxelEntityData>.FromSuccess(_migrator.Migrate(entity, dataVersion));
        }
        catch (Exception ex)
        {
            return Result<VoxelEntityData>.FromFailure($"Failed to deserialize voxel entity: {ex.Message}");
        }
    }

    private void WriteLevel(SqliteConnection connection, in Level level)
    {
        using SqliteCommand command = _levelTable.CreateCommand(connection);
        command.CommandText = """
            INSERT INTO level (guid, data) VALUES ($guid, $data)
            ON CONFLICT(guid) DO UPDATE SET data = excluded.data;
            """;
        command.Parameters.AddWithValue("$guid", _levelGuid);
        command.Parameters.AddWithValue("$data", level.Serialize());
        command.ExecuteNonQuery();
    }

    private void WriteCharacterEntity(SqliteConnection connection, in CharacterEntityData characterEntity)
    {
        using SqliteCommand command = _charactersTable.CreateCommand(connection);
        command.CommandText = """
            INSERT INTO character_locations (character_uuid, data) VALUES ($characterUuid, $data)
            ON CONFLICT(character_uuid) DO UPDATE SET data = excluded.data;
            """;
        command.Parameters.AddWithValue("$characterUuid", SqliteDatabase.EncodeKey(characterEntity.Uuid));
        command.Parameters.AddWithValue("$data", characterEntity.Serialize());
        command.ExecuteNonQuery();
    }
}