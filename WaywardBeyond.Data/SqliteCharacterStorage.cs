using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.Data.Sqlite;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data;

/// <summary>
/// SQLite-backed character store in the shared <c>profile.db</c>. Character records are raw nsd blobs;
/// the version gate and migrator match the previous KV-backed store.
/// </summary>
public class SqliteCharacterStorage : ICharacterStorage
{
    private const string CREATE_SCHEMA = "CREATE TABLE IF NOT EXISTS characters (id TEXT PRIMARY KEY, data BLOB NOT NULL);";

    private readonly StoragePaths _paths;
    private readonly SaveMigrator _migrator;
    private readonly Lock _initLock = new();
    private bool _initialized;

    public SqliteCharacterStorage(in StoragePaths paths, SaveMigrator? migrator = null)
    {
        _paths = paths;
        _migrator = migrator ?? CharacterSaveMigrations.Create();
    }

    public Result<Character> GetCharacter(ulong id)
    {
        try
        {
            EnsureInitialized();

            using SqliteConnection connection = SqliteDatabase.Open(_paths.ProfileDatabasePath);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT data FROM characters WHERE id = $id LIMIT 1;";
            command.Parameters.AddWithValue("$id", ToKey(id));

            if (command.ExecuteScalar() is not byte[] data || data.Length == 0)
            {
                return Result<Character>.FromFailure("Character not found.");
            }

            return Deserialize(data);
        }
        catch (Exception ex)
        {
            return Result<Character>.FromFailure(ex);
        }
    }

    public IEnumerable<Character> GetAllCharacters()
    {
        var characters = new List<Character>();

        try
        {
            EnsureInitialized();

            using SqliteConnection connection = SqliteDatabase.Open(_paths.ProfileDatabasePath);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT data FROM characters;";

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                byte[] data = (byte[])reader["data"];
                Result<Character> result = Deserialize(data);
                if (result.Success)
                {
                    characters.Add(result.Value);
                }
            }
        }
        catch
        {
            return characters;
        }

        return characters;
    }

    public Result SaveCharacter(Character character)
    {
        try
        {
            EnsureInitialized();

            byte[] data = character.Serialize();
            using SqliteConnection connection = SqliteDatabase.Open(_paths.ProfileDatabasePath);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO characters (id, data) VALUES ($id, $data)
                ON CONFLICT(id) DO UPDATE SET data = excluded.data;
                """;
            command.Parameters.AddWithValue("$id", ToKey(character.Id));
            command.Parameters.AddWithValue("$data", data);
            command.ExecuteNonQuery();

            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return Result.FromFailure($"Failed to serialize character for saving: {ex.Message}");
        }
    }

    public Result DeleteCharacter(ulong id)
    {
        try
        {
            EnsureInitialized();

            using SqliteConnection connection = SqliteDatabase.Open(_paths.ProfileDatabasePath);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM characters WHERE id = $id;";
            command.Parameters.AddWithValue("$id", ToKey(id));
            command.ExecuteNonQuery();

            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }
    }

    private Result<Character> Deserialize(byte[] data)
    {
        try
        {
            Character character = Character.Deserialize(data);
            if (!_migrator.IsSupported(character.Version.DataVersion))
            {
                return Result<Character>.FromFailure(
                    $"Character {character.Id} uses data version {character.Version.DataVersion}, which is newer than the supported format version {SaveVersion.CURRENT_DATA_VERSION}.");
            }

            return Result<Character>.FromSuccess(_migrator.Migrate(character, character.Version.DataVersion));
        }
        catch (SaveDataNotSupportedException ex)
        {
            return Result<Character>.FromFailure($"Failed to load character: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<Character>.FromFailure($"Failed to deserialize character: {ex.Message}");
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

    private static string ToKey(ulong value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
