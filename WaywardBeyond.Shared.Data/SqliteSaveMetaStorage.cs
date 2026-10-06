using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Data.Sqlite;
using Swordfish.Library.Util;

namespace WaywardBeyond.Shared.Data;

/// <summary>
/// SQLite-backed client save-listing metadata in the shared <c>profile.db</c>. Each row is a level
/// guid keyed to a serialized <see cref="SaveMeta"/>.
/// </summary>
public sealed class SqliteSaveMetaStorage : ISaveMetaStorage
{
    private const string CREATE_SCHEMA = "CREATE TABLE IF NOT EXISTS save_meta (level_guid TEXT PRIMARY KEY, data BLOB NOT NULL);";

    private readonly StoragePaths _paths;
    private readonly Lock _initLock = new();
    private bool _initialized;

    public SqliteSaveMetaStorage(in StoragePaths paths)
    {
        _paths = paths;
    }

    public Result<SaveMeta> Get(string levelGuid)
    {
        try
        {
            EnsureInitialized();

            using SqliteConnection connection = SqliteDatabase.Open(_paths.ProfileDatabasePath);
            using SqliteCommand command = connection.CreateCommand();
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

    public IEnumerable<KeyValuePair<string, SaveMeta>> GetAll()
    {
        var metas = new List<KeyValuePair<string, SaveMeta>>();

        try
        {
            EnsureInitialized();

            using SqliteConnection connection = SqliteDatabase.Open(_paths.ProfileDatabasePath);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT level_guid, data FROM save_meta;";

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string levelGuid = reader.GetString(0);
                try
                {
                    SaveMeta meta = SaveMeta.Deserialize((byte[])reader["data"]);
                    metas.Add(new KeyValuePair<string, SaveMeta>(levelGuid, meta));
                }
                catch
                {
                    //  Skip a corrupt row rather than failing the whole listing.
                }
            }
        }
        catch
        {
            return metas;
        }

        return metas;
    }

    public Result Save(string levelGuid, SaveMeta meta)
    {
        try
        {
            EnsureInitialized();

            byte[] data = meta.Serialize();
            using SqliteConnection connection = SqliteDatabase.Open(_paths.ProfileDatabasePath);
            using SqliteCommand command = connection.CreateCommand();
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
            return Result.FromFailure($"Failed to serialize save meta for saving: {ex.Message}");
        }
    }

    public Result Delete(string levelGuid)
    {
        try
        {
            EnsureInitialized();

            using SqliteConnection connection = SqliteDatabase.Open(_paths.ProfileDatabasePath);
            using SqliteCommand command = connection.CreateCommand();
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

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        using Lock.Scope _ = _initLock.EnterScope();
        if (_initialized)
        {
            return;
        }

        using SqliteConnection connection = SqliteDatabase.Open(_paths.ProfileDatabasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = CREATE_SCHEMA;
        command.ExecuteNonQuery();

        _initialized = true;
    }
}
