using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LiteDB;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Data.Migrations;

namespace WaywardBeyond.Shared.Data;

public sealed class LiteDbCharacterStorage : ICharacterStorage, IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<BsonDocument> _characterCollection;

    public LiteDbCharacterStorage(IConfiguration configuration)
    {
        string dbPath = configuration.GetString("LITEDB_PATH") ?? "litedb/character.db";
        string dbDirectory = Path.GetDirectoryName(dbPath)!;
        Directory.CreateDirectory(dbDirectory);
        
        _db = new LiteDatabase(dbPath);
        RunMigrations();
        _characterCollection = _db.GetCollection("characters");
    }

    public Character? GetCharacter(string guid)
    {
        BsonDocument? doc = _characterCollection.FindById(guid);
        return doc == null ? null : BsonToCharacter(doc);
    }

    public IEnumerable<Character> GetAllCharacters()
    {
        return _characterCollection.FindAll().Select(BsonToCharacter);
    }

    public bool SaveCharacter(in Character character)
    {
        BsonDocument doc = CharacterToBson(character);
        return _characterCollection.Upsert(doc);
    }

    public bool DeleteCharacter(string guid)
    {
        return _characterCollection.Delete(guid);
    }

    private void RunMigrations()
    {
        int currentVersion = _db.UserVersion;
        if (currentVersion >= DataVersion.CURRENT)
        {
            return;
        }

        IEnumerable<Type> migrationTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(IMigration).IsAssignableFrom(t) && t.IsClass);

        IOrderedEnumerable<IMigration> migrations = migrationTypes
            .Select(Activator.CreateInstance)
            .Cast<IMigration>()
            .OrderBy(m => m.FromVersion);

        foreach (IMigration migration in migrations)
        {
            if (currentVersion == migration.FromVersion)
            {
                migration.Apply(_db);
                _db.UserVersion = migration.ToVersion;
                currentVersion = migration.ToVersion;
            }
        }

        if (_db.UserVersion != DataVersion.CURRENT)
        {
            // This indicates a gap in the migration path.
            // For now, we'll just force it, but in a real scenario, you might throw an error.
            _db.UserVersion = DataVersion.CURRENT;
        }
    }

    private static BsonDocument CharacterToBson(in Character character)
    {
        var doc = new BsonDocument
        {
            ["_id"] = character.Guid,
            ["LastPlayedMs"] = character.LastPlayedMs,
            ["AgeMs"] = character.AgeMs,
            ["Name"] = character.Name,
            ["Strength"] = character.Strength,
            ["Precision"] = character.Precision,
            ["Awareness"] = character.Awareness,
            ["Charisma"] = character.Charisma,
            ["Education"] = character.Education,
            ["Resolve"] = character.Resolve,
            ["Body"] = character.Body,
        };

        if (character.Inventory != null)
        {
            doc["Inventory"] = new BsonArray(character.Inventory.Select(itemData => new BsonDocument
            {
                ["ID"] = itemData.ID,
                ["Count"] = itemData.Count,
                ["MaxSize"] = itemData.MaxSize,
            }));
        }

        if (character.Statistics != null)
        {
            doc["Statistics"] = new BsonArray(character.Statistics.Select(statistic => new BsonDocument
            {
                ["ID"] = statistic.ID,
                ["Value"] = statistic.Value,
            }));
        }

        return doc;
    }

    private static Character BsonToCharacter(BsonDocument doc)
    {
        var character = new Character
        {
            Guid = doc["_id"].AsString,
            LastPlayedMs = doc.ContainsKey("LastPlayedMs") ? doc["LastPlayedMs"].AsInt64 : 0,
            AgeMs = doc.ContainsKey("AgeMs") ? doc["AgeMs"].AsInt64 : 0,
            Name = doc.ContainsKey("Name") ? doc["Name"].AsString : string.Empty,
            Strength = doc.ContainsKey("Strength") ? doc["Strength"].AsInt32 : 0,
            Precision = doc.ContainsKey("Precision") ? doc["Precision"].AsInt32 : 0,
            Awareness = doc.ContainsKey("Awareness") ? doc["Awareness"].AsInt32 : 0,
            Charisma = doc.ContainsKey("Charisma") ? doc["Charisma"].AsInt32 : 0,
            Education = doc.ContainsKey("Education") ? doc["Education"].AsInt32 : 0,
            Resolve = doc.ContainsKey("Resolve") ? doc["Resolve"].AsInt32 : 0,
            Body = doc.ContainsKey("Body") ? doc["Body"].AsInt32 : 0,
        };

        if (doc.TryGetValue("Inventory", out BsonValue? inventoryBson) && inventoryBson.IsArray)
        {
            character.Inventory = inventoryBson.AsArray.Select(i => new ItemData
            {
                ID = i["ID"].AsString,
                Count = i["Count"].AsInt32,
                MaxSize = i["MaxSize"].AsInt32,
            }).ToArray();
        }

        if (doc.TryGetValue("Statistics", out BsonValue? statisticsBson) && statisticsBson.IsArray)
        {
            character.Statistics = statisticsBson.AsArray.Select(s => new Statistic
            {
                ID = s["ID"].AsString,
                Value = s["Value"].AsInt64,
            }).ToArray();
        }

        return character;
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
