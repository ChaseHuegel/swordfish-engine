using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace WaywardBeyond.Data;

/// <summary>
/// SQLite-backed per-level save store. Writes serialize on an internal lock and the full snapshot
/// commits in one transaction. Entity rows are replaced by each snapshot; character location rows are
/// upsert-only so a character absent from the world at save time keeps its last location.
/// </summary>
public sealed class SqliteLevelStore : ILevelStore
{
    private const string CREATE_SCHEMA = """
        CREATE TABLE IF NOT EXISTS level (guid TEXT PRIMARY KEY, data BLOB NOT NULL);
        CREATE TABLE IF NOT EXISTS entities (uuid TEXT PRIMARY KEY, data BLOB NOT NULL);
        CREATE TABLE IF NOT EXISTS character_locations (character_id TEXT PRIMARY KEY, data BLOB NOT NULL);
        """;

    private readonly string _levelGuid;
    private readonly string _databasePath;
    private readonly Lock _writeLock = new();
    private bool _disposed;

    public SqliteLevelStore(in StoragePaths paths, string levelGuid)
    {
        _levelGuid = levelGuid;
        _databasePath = paths.LevelDatabasePath(levelGuid);

        using SqliteConnection connection = SqliteDatabase.Open(_databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = CREATE_SCHEMA;
        command.ExecuteNonQuery();
    }

    public byte[]? ReadLevel()
    {
        using SqliteConnection connection = SqliteDatabase.Open(_databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT data FROM level LIMIT 1;";
        return command.ExecuteScalar() as byte[];
    }

    public IReadOnlyList<LevelEntityRecord> ReadEntities()
    {
        using SqliteConnection connection = SqliteDatabase.Open(_databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT uuid, data FROM entities;";

        var records = new List<LevelEntityRecord>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string uuidText = reader.GetString(0);
            if (!ulong.TryParse(uuidText, NumberStyles.None, CultureInfo.InvariantCulture, out ulong uuid))
            {
                continue;
            }

            records.Add(new LevelEntityRecord(uuid, (byte[])reader["data"]));
        }

        return records;
    }

    public byte[]? ReadLocation(ulong characterId)
    {
        using SqliteConnection connection = SqliteDatabase.Open(_databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT data FROM character_locations WHERE character_id = $characterId LIMIT 1;";
        command.Parameters.AddWithValue("$characterId", ToKey(characterId));
        return command.ExecuteScalar() as byte[];
    }

    public void WriteSave(byte[] levelData, IReadOnlyList<LevelEntityRecord> entities, IReadOnlyList<LevelLocationRecord> locations)
    {
        using Lock.Scope _ = _writeLock.EnterScope();
        ThrowIfDisposed();

        using SqliteConnection connection = SqliteDatabase.Open(_databasePath);
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);

        WriteLevel(connection, transaction, levelData);

        using (SqliteCommand delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM entities;";
            delete.ExecuteNonQuery();
        }

        foreach (LevelEntityRecord entity in entities)
        {
            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO entities (uuid, data) VALUES ($uuid, $data);";
            insert.Parameters.AddWithValue("$uuid", ToKey(entity.Uuid));
            insert.Parameters.AddWithValue("$data", entity.Data);
            insert.ExecuteNonQuery();
        }

        foreach (LevelLocationRecord location in locations)
        {
            WriteLocation(connection, transaction, location.CharacterId, location.Data);
        }

        transaction.Commit();
    }

    public void WriteLevel(byte[] levelData)
    {
        using Lock.Scope _ = _writeLock.EnterScope();
        ThrowIfDisposed();

        using SqliteConnection connection = SqliteDatabase.Open(_databasePath);
        WriteLevel(connection, null, levelData);
    }

    public void WriteLocation(ulong characterId, byte[] data)
    {
        using Lock.Scope _ = _writeLock.EnterScope();
        ThrowIfDisposed();

        using SqliteConnection connection = SqliteDatabase.Open(_databasePath);
        WriteLocation(connection, null, characterId, data);
    }

    public void Dispose()
    {
        using Lock.Scope _ = _writeLock.EnterScope();
        _disposed = true;
    }

    private void WriteLevel(SqliteConnection connection, SqliteTransaction? transaction, byte[] levelData)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO level (guid, data) VALUES ($guid, $data)
            ON CONFLICT(guid) DO UPDATE SET data = excluded.data;
            """;
        command.Parameters.AddWithValue("$guid", _levelGuid);
        command.Parameters.AddWithValue("$data", levelData);
        command.ExecuteNonQuery();
    }

    private static void WriteLocation(SqliteConnection connection, SqliteTransaction? transaction, ulong characterId, byte[] data)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO character_locations (character_id, data) VALUES ($characterId, $data)
            ON CONFLICT(character_id) DO UPDATE SET data = excluded.data;
            """;
        command.Parameters.AddWithValue("$characterId", ToKey(characterId));
        command.Parameters.AddWithValue("$data", data);
        command.ExecuteNonQuery();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SqliteLevelStore));
        }
    }

    private static string ToKey(ulong value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
