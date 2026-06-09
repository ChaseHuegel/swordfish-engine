using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Config;

namespace WaywardBeyond.Shared.Data;

public sealed class SqliteCharacterStorage : ICharacterStorage, IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteCharacterStorage(IConfiguration configuration)
    {
        string dbPath = configuration.GetString("SQLITE_PATH") ?? "sqlite/character.db";
        
        string dbDirectory = Path.GetDirectoryName(dbPath)!;
        if (!string.IsNullOrEmpty(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }
        
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        
        using (SqliteCommand command = _connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = """
                              CREATE TABLE IF NOT EXISTS characters (
                                  guid            TEXT PRIMARY KEY,
                                  name            TEXT NOT NULL,
                                  last_played_ms  INTEGER NOT NULL,
                                  age_ms          INTEGER NOT NULL,
                                  strength        INTEGER NOT NULL,
                                  precision       INTEGER NOT NULL,
                                  awareness       INTEGER NOT NULL,
                                  charisma        INTEGER NOT NULL,
                                  education       INTEGER NOT NULL,
                                  resolve         INTEGER NOT NULL,
                                  body            INTEGER NOT NULL
                              );

                              CREATE TABLE IF NOT EXISTS inventory_items (
                                  character_guid  TEXT NOT NULL,
                                  slot_index      INTEGER NOT NULL,
                                  item_id         TEXT NOT NULL,
                                  count           INTEGER NOT NULL,
                                  max_size        INTEGER NOT NULL,
                                  PRIMARY KEY (character_guid, slot_index),
                                  FOREIGN KEY (character_guid) REFERENCES characters (guid) ON DELETE CASCADE
                              );

                              CREATE TABLE IF NOT EXISTS character_statistics (
                                  character_guid  TEXT NOT NULL,
                                  statistic_id    TEXT NOT NULL,
                                  value           INTEGER NOT NULL,
                                  PRIMARY KEY (character_guid, statistic_id),
                                  FOREIGN KEY (character_guid) REFERENCES characters (guid) ON DELETE CASCADE
                              );
                              """;
        command.ExecuteNonQuery();
    }

    public Result<Character> GetCharacter(string guid)
    {
        Character character;

        using (SqliteCommand command = _connection.CreateCommand())
        {
            command.CommandText = "SELECT guid, name, last_played_ms, age_ms, strength, precision, awareness, charisma, education, resolve, body FROM characters WHERE guid = @guid;";
            command.Parameters.AddWithValue("@guid", guid);

            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return Result<Character>.FromFailure("Character not found.");
            }

            character = new Character
            {
                Guid = reader.GetString(0),
                Name = reader.GetString(1),
                LastPlayedMs = reader.GetInt64(2),
                AgeMs = reader.GetInt64(3),
                Strength = reader.GetInt32(4),
                Precision = reader.GetInt32(5),
                Awareness = reader.GetInt32(6),
                Charisma = reader.GetInt32(7),
                Education = reader.GetInt32(8),
                Resolve = reader.GetInt32(9),
                Body = reader.GetInt32(10),
            };
        }

        character.Inventory = GetInventory(guid);
        character.Statistics = GetStatistics(guid);

        return Result<Character>.FromSuccess(character);
    }

    public IEnumerable<Character> GetAllCharacters()
    {
        var guids = new List<string>();
        using (SqliteCommand command = _connection.CreateCommand())
        {
            command.CommandText = "SELECT guid FROM characters;";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                guids.Add(reader.GetString(0));
            }
        }

        foreach (string guid in guids)
        {
            Character? character = GetCharacter(guid);
            if (character.HasValue)
            {
                yield return character.Value;
            }
        }
    }

    public Result SaveCharacter(in Character character)
    {
        using SqliteTransaction transaction = _connection.BeginTransaction();
        
        try
        {
            using (SqliteCommand command = _connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                                      INSERT OR REPLACE INTO characters (
                                          guid, name, last_played_ms, age_ms, strength, precision, 
                                          awareness, charisma, education, resolve, body
                                      ) VALUES (
                                          @guid, @name, @last_played_ms, @age_ms, @strength, @precision, 
                                          @awareness, @charisma, @education, @resolve, @body
                                      );
                                      """;
                command.Parameters.AddWithValue("@guid", character.Guid);
                command.Parameters.AddWithValue("@name", character.Name);
                command.Parameters.AddWithValue("@last_played_ms", character.LastPlayedMs);
                command.Parameters.AddWithValue("@age_ms", character.AgeMs);
                command.Parameters.AddWithValue("@strength", character.Strength);
                command.Parameters.AddWithValue("@precision", character.Precision);
                command.Parameters.AddWithValue("@awareness", character.Awareness);
                command.Parameters.AddWithValue("@charisma", character.Charisma);
                command.Parameters.AddWithValue("@education", character.Education);
                command.Parameters.AddWithValue("@resolve", character.Resolve);
                command.Parameters.AddWithValue("@body", character.Body);
                command.ExecuteNonQuery();
            }

            ClearSubTable("inventory_items", character.Guid, transaction);
            if (character.Inventory != null)
            {
                for (var i = 0; i < character.Inventory.Length; i++)
                {
                    ItemData item = character.Inventory[i];
                    if (item.ID == null)
                    {
                        continue;
                    }
                    
                    using SqliteCommand command = _connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "INSERT INTO inventory_items (character_guid, slot_index, item_id, count, max_size) VALUES (@guid, @slot, @id, @count, @max);";
                    command.Parameters.AddWithValue("@guid", character.Guid);
                    command.Parameters.AddWithValue("@slot", i);
                    command.Parameters.AddWithValue("@id", item.ID);
                    command.Parameters.AddWithValue("@count", item.Count);
                    command.Parameters.AddWithValue("@max", item.MaxSize);
                    command.ExecuteNonQuery();
                }
            }

            ClearSubTable("character_statistics", character.Guid, transaction);
            if (character.Statistics != null)
            {
                foreach (Statistic statistic in character.Statistics)
                {
                    if (statistic.ID == null)
                    {
                        continue;
                    }
                    
                    using SqliteCommand command = _connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "INSERT INTO character_statistics (character_guid, statistic_id, value) VALUES (@guid, @id, @val);";
                    command.Parameters.AddWithValue("@guid", character.Guid);
                    command.Parameters.AddWithValue("@id", statistic.ID);
                    command.Parameters.AddWithValue("@val", statistic.Value);
                    command.ExecuteNonQuery();
                }
            }

            transaction.Commit();
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return Result.FromFailure(ex.ToString());
        }
    }

    public Result DeleteCharacter(string guid)
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM characters WHERE guid = @guid;";
        command.Parameters.AddWithValue("@guid", guid);
        
        return new Result(success: command.ExecuteNonQuery() > 0);
    }

    private ItemData[] GetInventory(string guid)
    {
        var items = new List<ItemData>();
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT item_id, count, max_size FROM inventory_items WHERE character_guid = @guid ORDER BY slot_index;";
        command.Parameters.AddWithValue("@guid", guid);
        
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new ItemData
            {
                ID = reader.GetString(0),
                Count = reader.GetInt32(1),
                MaxSize = reader.GetInt32(2),
            });
        }
        
        return items.ToArray();
    }

    private Statistic[] GetStatistics(string guid)
    {
        var stats = new List<Statistic>();
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT statistic_id, value FROM character_statistics WHERE character_guid = @guid;";
        command.Parameters.AddWithValue("@guid", guid);
        
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            stats.Add(new Statistic
            {
                ID = reader.GetString(0),
                Value = reader.GetInt64(1),
            });
        }
        
        return stats.ToArray();
    }
    
    private void ClearSubTable(string tableName, string guid, SqliteTransaction transaction)
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"DELETE FROM {tableName} WHERE character_guid = @guid;";
        command.Parameters.AddWithValue("@guid", guid);
        
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}
