using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WaywardBeyond.Shared.Config;

namespace WaywardBeyond.Shared.Data;

public sealed class SqliteCharacterStorage : ICharacterStorage, IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteCharacterStorage(IConfiguration configuration)
    {
        string dbPath = configuration.GetString("SQLITE_PATH") ?? "sqlite/character.db";
        string dbDirectory = Path.GetDirectoryName(dbPath)!;
        Directory.CreateDirectory(dbDirectory);
        
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS characters (
                Guid TEXT PRIMARY KEY,
                LastPlayedMs INTEGER NOT NULL,
                AgeMs INTEGER NOT NULL,
                Name TEXT NOT NULL,
                Strength INTEGER NOT NULL,
                Precision INTEGER NOT NULL,
                Awareness INTEGER NOT NULL,
                Charisma INTEGER NOT NULL,
                Education INTEGER NOT NULL,
                Resolve INTEGER NOT NULL,
                Body INTEGER NOT NULL,
                Inventory TEXT,
                Statistics TEXT
            );
        ";
        command.ExecuteNonQuery();
    }

    public Character? GetCharacter(string guid)
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT * FROM characters WHERE Guid = @Guid;";
        command.Parameters.AddWithValue("@Guid", guid);

        using SqliteDataReader reader = command.ExecuteReader();
        if (reader.Read())
        {
            return ReadCharacter(reader);
        }
        return null;
    }

    public IEnumerable<Character> GetAllCharacters()
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT * FROM characters;";
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return ReadCharacter(reader);
        }
    }

    public bool SaveCharacter(in Character character)
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = """
                              INSERT OR REPLACE INTO characters (
                                  Guid, LastPlayedMs, AgeMs, Name, Strength, Precision, Awareness, 
                                  Charisma, Education, Resolve, Body, Inventory, Statistics
                              ) VALUES (
                                  @Guid, @LastPlayedMs, @AgeMs, @Name, @Strength, @Precision, @Awareness, 
                                  @Charisma, @Education, @Resolve, @Body, @Inventory, @Statistics
                              );
                              """;

        command.Parameters.AddWithValue("@Guid", character.Guid);
        command.Parameters.AddWithValue("@LastPlayedMs", character.LastPlayedMs);
        command.Parameters.AddWithValue("@AgeMs", character.AgeMs);
        command.Parameters.AddWithValue("@Name", character.Name);
        command.Parameters.AddWithValue("@Strength", character.Strength);
        command.Parameters.AddWithValue("@Precision", character.Precision);
        command.Parameters.AddWithValue("@Awareness", character.Awareness);
        command.Parameters.AddWithValue("@Charisma", character.Charisma);
        command.Parameters.AddWithValue("@Education", character.Education);
        command.Parameters.AddWithValue("@Resolve", character.Resolve);
        command.Parameters.AddWithValue("@Body", character.Body);
        command.Parameters.AddWithValue("@Inventory", character.Inventory != null ? JsonSerializer.Serialize(character.Inventory) : DBNull.Value);
        command.Parameters.AddWithValue("@Statistics", character.Statistics != null ? JsonSerializer.Serialize(character.Statistics) : DBNull.Value);

        return command.ExecuteNonQuery() > 0;
    }

    public bool DeleteCharacter(string guid)
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM characters WHERE Guid = @Guid;";
        command.Parameters.AddWithValue("@Guid", guid);
        return command.ExecuteNonQuery() > 0;
    }

    private static Character ReadCharacter(SqliteDataReader reader)
    {
        return new Character
        {
            Guid = reader.GetString(0),
            LastPlayedMs = reader.GetInt64(1),
            AgeMs = reader.GetInt64(2),
            Name = reader.GetString(3),
            Strength = reader.GetInt32(4),
            Precision = reader.GetInt32(5),
            Awareness = reader.GetInt32(6),
            Charisma = reader.GetInt32(7),
            Education = reader.GetInt32(8),
            Resolve = reader.GetInt32(9),
            Body = reader.GetInt32(10),
            Inventory = !reader.IsDBNull(11) ? JsonSerializer.Deserialize<ItemData[]>(reader.GetString(11)) : [],
            Statistics = !reader.IsDBNull(12) ? JsonSerializer.Deserialize<Statistic[]>(reader.GetString(12)) : [],
        };
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}
