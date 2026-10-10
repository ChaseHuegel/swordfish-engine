using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Migrations;
using WaywardBeyond.Data.Sqlite;

namespace WaywardBeyond.Data.Characters;

/// <summary>Provides access to sqlite-based character data.</summary>
public sealed class SqliteCharacterStorage(
    in ILogger<SqliteCharacterStorage> logger,
    in StoragePaths paths,
    in Migrator migrator
) : ICharacterStorage
{
    private const string CREATE_TABLE_COMMAND = "CREATE TABLE IF NOT EXISTS characters (uuid TEXT PRIMARY KEY, data BLOB NOT NULL);";

    private readonly Migrator _migrator = migrator;
    private readonly ILogger _logger = logger;
    private readonly SqliteTable _table = new(paths.ProfileDatabasePath, CREATE_TABLE_COMMAND);

    /// <inheritdoc/>
    public Result<Character> GetCharacter(ulong uuid)
    {
        try
        {
            using SqliteCommand command = _table.CreateCommand();
            command.CommandText = "SELECT data FROM characters WHERE uuid = $uuid LIMIT 1;";
            command.Parameters.AddWithValue("$uuid", SqliteDatabase.EncodeKey(uuid));

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

    /// <inheritdoc/>
    public IReadOnlyList<Character> GetAllCharacters()
    {
        var characters = new List<Character>();

        try
        {
            using SqliteCommand command = _table.CreateCommand();
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
                else
                {
                    _logger.LogWarning("Skipping an unreadable character: {message}", result.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list characters.");
        }

        return characters;
    }

    /// <inheritdoc/>
    public Result SaveCharacter(Character character)
    {
        try
        {
            byte[] data = character.Serialize();
            
            using SqliteCommand command = _table.CreateCommand();
            command.CommandText = """
                INSERT INTO characters (uuid, data) VALUES ($uuid, $data)
                ON CONFLICT(uuid) DO UPDATE SET data = excluded.data;
                """;
            command.Parameters.AddWithValue("$uuid", SqliteDatabase.EncodeKey(character.Uuid));
            command.Parameters.AddWithValue("$data", data);
            command.ExecuteNonQuery();

            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return new Result(false, $"Failed to save character: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public Result DeleteCharacter(ulong uuid)
    {
        try
        {
            using SqliteCommand command = _table.CreateCommand();
            command.CommandText = "DELETE FROM characters WHERE uuid = $uuid;";
            command.Parameters.AddWithValue("$uuid", SqliteDatabase.EncodeKey(uuid));
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
            if (!_migrator.IsSupported<Character>(character.Version.DataVersion))
            {
                return Result<Character>.FromFailure($"Character {character.Uuid} uses data version {character.Version.DataVersion}, which is newer than the supported format version {SaveVersion.CURRENT_DATA_VERSION}.");
            }

            return Result<Character>.FromSuccess(_migrator.Migrate(character, character.Version.DataVersion));
        }
        catch (Exception ex)
        {
            return Result<Character>.FromFailure($"Failed to deserialize character: {ex.Message}");
        }
    }
}