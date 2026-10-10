using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.Data.Sqlite;
using WaywardBeyond.Data.Sqlite;

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
        CREATE TABLE IF NOT EXISTS character_locations (character_uuid TEXT PRIMARY KEY, data BLOB NOT NULL);
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
        ThrowIfDisposed();

        using SqliteConnection connection = SqliteDatabase.Open(_databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT data FROM level LIMIT 1;";
        return command.ExecuteScalar() as byte[];
    }

    public IReadOnlyList<LevelEntityRecord> ReadEntities()
    {
        ThrowIfDisposed();

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

    public byte[]? ReadLocation(ulong characterUuid)
    {
        ThrowIfDisposed();

        using SqliteConnection connection = SqliteDatabase.Open(_databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT data FROM character_locations WHERE character_uuid = $characterUuid LIMIT 1;";
        command.Parameters.AddWithValue("$characterUuid", SqliteDatabase.EncodeKey(characterUuid));
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

        using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO entities (uuid, data) VALUES ($uuid, $data);";
            insert.Parameters.Add("$uuid", SqliteType.Text);
            insert.Parameters.Add("$data", SqliteType.Blob);

            foreach (LevelEntityRecord entity in entities)
            {
                insert.Parameters["$uuid"].Value = SqliteDatabase.EncodeKey(entity.Uuid);
                insert.Parameters["$data"].Value = entity.Data;
                insert.ExecuteNonQuery();
            }
        }

        foreach (LevelLocationRecord location in locations)
        {
            WriteLocation(connection, transaction, location.CharacterUuid, location.Data);
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

    public void WriteLocation(ulong characterUuid, byte[] data)
    {
        using Lock.Scope _ = _writeLock.EnterScope();
        ThrowIfDisposed();

        using SqliteConnection connection = SqliteDatabase.Open(_databasePath);
        WriteLocation(connection, null, characterUuid, data);
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

    private static void WriteLocation(SqliteConnection connection, SqliteTransaction? transaction, ulong characterUuid, byte[] data)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO character_locations (character_uuid, data) VALUES ($characterUuid, $data)
            ON CONFLICT(character_uuid) DO UPDATE SET data = excluded.data;
            """;
        command.Parameters.AddWithValue("$characterUuid", SqliteDatabase.EncodeKey(characterUuid));
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
}
