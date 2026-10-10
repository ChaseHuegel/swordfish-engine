using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Sqlite;

namespace WaywardBeyond.Data.Levels;

/// <summary>Provides access to sqlite-based level metadata.</summary>
public sealed class SqliteLevelMetadataStorage(in ILogger<SqliteLevelMetadataStorage> logger, in StoragePaths paths) : ILevelMetadataStorage
{
    private const string CREATE_TABLE_COMMAND = "CREATE TABLE IF NOT EXISTS save_meta (level_guid TEXT PRIMARY KEY, data BLOB NOT NULL);";

    private readonly ILogger _logger = logger;
    private readonly SqliteTable _table = new(paths.ProfileDatabasePath, CREATE_TABLE_COMMAND);

    /// <inheritdoc/>
    public Result<SaveMeta> Get(string levelGuid)
    {
        try
        {
            using SqliteCommand command = _table.CreateCommand();
            command.CommandText = "SELECT data FROM save_meta WHERE level_guid = $guid LIMIT 1;";
            command.Parameters.AddWithValue("$guid", levelGuid);

            if (command.ExecuteScalar() is not byte[] data || data.Length == 0)
            {
                return Result<SaveMeta>.FromFailure("Save meta not found.");
            }

            return Result<SaveMeta>.FromSuccess(SaveMeta.Deserialize(data));
        }
        catch (Exception ex)
        {
            return Result<SaveMeta>.FromFailure(ex);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, SaveMeta> GetAll()
    {
        var metas = new Dictionary<string, SaveMeta>();

        try
        {
            using SqliteCommand command = _table.CreateCommand();
            command.CommandText = "SELECT level_guid, data FROM save_meta;";

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string levelGuid = reader.GetString(0);
                try
                {
                    SaveMeta meta = SaveMeta.Deserialize((byte[])reader["data"]);
                    metas[levelGuid] = meta;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Skipping corrupt metadata for level \"{guid}\".", levelGuid);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list level metadata.");
        }

        return metas;
    }

    /// <inheritdoc/>
    public Result Save(string levelGuid, SaveMeta meta)
    {
        try
        {
            byte[] data = meta.Serialize();
            
            using SqliteCommand command = _table.CreateCommand();
            command.CommandText = """
                INSERT INTO save_meta (level_guid, data) VALUES ($guid, $data)
                ON CONFLICT(level_guid) DO UPDATE SET data = excluded.data;
                """;
            command.Parameters.AddWithValue("$guid", levelGuid);
            command.Parameters.AddWithValue("$data", data);
            command.ExecuteNonQuery();

            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return new Result(false, $"Failed to save level metadata: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public Result Delete(string levelGuid)
    {
        try
        {
            using SqliteCommand command = _table.CreateCommand();
            command.CommandText = "DELETE FROM save_meta WHERE level_guid = $guid;";
            command.Parameters.AddWithValue("$guid", levelGuid);
            command.ExecuteNonQuery();

            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }
    }
}