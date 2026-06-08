using System;
using System.Collections.Generic;
using System.Linq;
using NATS.Client.KeyValueStore;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Globalization;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.UI;
using WaywardBeyond.Server.Core.Streaming;

namespace WaywardBeyond.Client.Core.Saves;

internal sealed class CharacterSaveService(in LocalizedFormatter localizedFormatter, in NotificationService notificationService, in IECSContext ecs, in KeyValueStore kvStore)
{
    private const string CHARACTERS_BUCKET = "characters";
    private const string CHARACTER_DATA_BUCKET = "characterData";

    private readonly LocalizedFormatter _localizedFormatter = localizedFormatter;
    private readonly NotificationService _notificationService = notificationService;
    private readonly IECSContext _ecs = ecs;
    private readonly KeyValueStore _kvStore = kvStore;

    public CharacterSave[] GetSaves()
    {
        Result<NatsKVEntry<byte[]>[]> getAllResult = _kvStore.GetAll<byte[]>(CHARACTERS_BUCKET);
        if (!getAllResult.Success)
        {
            return [];
        }

        var characters = new Dictionary<string, Character>();
        var statistics = new Dictionary<string, List<Statistic>>();

        foreach (NatsKVEntry<byte[]> entry in getAllResult.Value)
        {
            string[] parts = entry.Key.Split('.');
            if (parts.Length < 2) continue;

            string guid = parts[0];
            if (!characters.TryGetValue(guid, out Character character))
            {
                character = new Character { Guid = guid };
                characters[guid] = character;
            }

            if (parts.Length == 3 && parts[1] == "Statistic")
            {
                if (!statistics.TryGetValue(guid, out List<Statistic>? statList))
                {
                    statList = new List<Statistic>();
                    statistics[guid] = statList;
                }

                statList.Add(new Statistic { ID = parts[2], Value = BitConverter.ToInt64(entry.Value, 0) });
                continue;
            }
            
            string property = parts[1];
            switch (property)
            {
                case "Name":
                    character.Name = System.Text.Encoding.UTF8.GetString(entry.Value);
                    break;
                case "LastPlayedMs":
                    character.LastPlayedMs = BitConverter.ToInt64(entry.Value, 0);
                    break;
                case "AgeMs":
                    character.AgeMs = BitConverter.ToInt64(entry.Value, 0);
                    break;
                case "Strength":
                    character.Strength = BitConverter.ToInt32(entry.Value, 0);
                    break;
                case "Precision":
                    character.Precision = BitConverter.ToInt32(entry.Value, 0);
                    break;
                case "Awareness":
                    character.Awareness = BitConverter.ToInt32(entry.Value, 0);
                    break;
                case "Charisma":
                    character.Charisma = BitConverter.ToInt32(entry.Value, 0);
                    break;
                case "Education":
                    character.Education = BitConverter.ToInt32(entry.Value, 0);
                    break;
                case "Resolve":
                    character.Resolve = BitConverter.ToInt32(entry.Value, 0);
                    break;
                case "Body":
                    character.Body = BitConverter.ToInt32(entry.Value, 0);
                    break;
                case "Inventory":
                    character.Inventory = _kvStore.Get<ItemData[]>(CHARACTER_DATA_BUCKET, entry.Key).Value;
                    break;
            }
        }

        foreach (Character character in characters.Values)
        {
            if (statistics.TryGetValue(character.Guid, out List<Statistic>? statList))
            {
                character.Statistics = statList.ToArray();
            }
        }

        return characters.Values.Select(c => new CharacterSave(c)).ToArray();
    }
    
    public Result<CharacterSave> CreateSave(Character character)
    {
        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        character.LastPlayedMs = nowUtcMs;
        character.AgeMs = 0;
        
        var characterSave = new CharacterSave(character);

        Result<CharacterSave> saveResult = Save(characterSave);
        if (!saveResult)
        {
            _notificationService.Push(_localizedFormatter.GetString("notification.character.create.failed", character.Name));
            return new Result<CharacterSave>(success: false, default, saveResult.Message, saveResult.Exception);
        }
        
        _notificationService.Push(_localizedFormatter.GetString("notification.character.created", character.Name));
        return Result<CharacterSave>.FromSuccess(saveResult.Value);
    }

    public Result<CharacterSave> Save(CharacterSave save)
    {
        try
        {
            Character character = save.Character;
            
            //  Update the character's inventory
            _ecs.World.DataStore.Query<CharacterComponent, TransformComponent>(0f, ForEachCharacterEntity);
            void ForEachCharacterEntity(float delta, DataStore store, int entity, ref CharacterComponent characterComponent, ref TransformComponent transform)
            {
                if (!store.TryGet(entity, out GuidComponent guidComponent))
                {
                    return;
                }
            
                if (!store.TryGet(entity, out InventoryComponent inventoryComponent))
                {
                    return;
                }

                if (guidComponent.Guid.ToString() != character.Guid)
                {
                    return;
                }

                character.Inventory = new ItemData[inventoryComponent.Contents.Length];
                for (var i = 0; i < inventoryComponent.Contents.Length; i++)
                {
                    ItemStack itemStack = inventoryComponent.Contents[i];
                    character.Inventory[i] = new ItemData(itemStack.ID, itemStack.Count, itemStack.MaxSize);
                } 
            }
            
            //  Save the character properties
            _kvStore.Put(CHARACTER_DATA_BUCKET, $"{character.Guid}.Name", character.Name);
            _kvStore.Put(CHARACTER_DATA_BUCKET, $"{character.Guid}.LastPlayedMs", character.LastPlayedMs);
            _kvStore.Put(CHARACTER_DATA_BUCKET, $"{character.Guid}.AgeMs", character.AgeMs);
            _kvStore.Put(CHARACTER_DATA_BUCKET, $"{character.Guid}.Strength", character.Strength);
            _kvStore.Put(CHARACTER_DATA_BUCKET, $"{character.Guid}.Precision", character.Precision);
            _kvStore.Put(CHARACTER_DATA_BUCKET, $"{character.Guid}.Awareness", character.Awareness);
            _kvStore.Put(CHARACTER_DATA_BUCKET, $"{character.Guid}.Charisma", character.Charisma);
            _kvStore.Put(CHARACTER_DATA_BUCKET, $"{character.Guid}.Education", character.Education);
            _kvStore.Put(CHARACTER_DATA_BUCKET, $"{character.Guid}.Resolve", character.Resolve);
            _kvStore.Put(CHARACTER_DATA_BUCKET, $"{character.Guid}.Body", character.Body);
            _kvStore.Put(CHARACTER_DATA_BUCKET, $"{character.Guid}.Inventory", character.Inventory);

            if (character.Statistics != null)
            {
                foreach (Statistic statistic in character.Statistics)
                {
                    _kvStore.Put(CHARACTER_DATA_BUCKET, $"{character.Guid}.Statistic.{statistic.ID}", statistic.Value);
                }
            }

            _notificationService.Push(_localizedFormatter.GetString("notification.character.saved", save.Character.Name));
            return Result<CharacterSave>.FromSuccess(new CharacterSave(character));
        }
        catch (Exception ex)
        {
            _notificationService.Push(_localizedFormatter.GetString("notification.character.saving.failed", save.Character.Name));
            return new Result<CharacterSave>(success: false, default, $"Unexpected error saving character \"{save.Character.Name}\".", ex);
        }
    }
}
