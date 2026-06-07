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

        IEnumerable<string> guids = getAllResult.Value
            .Select(entry => entry.Key.Split('.')[0])
            .Distinct();

        var characterSaves = new List<CharacterSave>();
        foreach (string guid in guids)
        {
            var character = new Character
            {
                Guid = guid,
                Name = _kvStore.Get<string>(CHARACTERS_BUCKET, $"{guid}.Name").Value ?? string.Empty,
                LastPlayedMs = _kvStore.Get<long>(CHARACTERS_BUCKET, $"{guid}.LastPlayedMs").Value,
                AgeMs = _kvStore.Get<long>(CHARACTERS_BUCKET, $"{guid}.AgeMs").Value,
                Strength = _kvStore.Get<int>(CHARACTERS_BUCKET, $"{guid}.Strength").Value,
                Precision = _kvStore.Get<int>(CHARACTERS_BUCKET, $"{guid}.Precision").Value,
                Awareness = _kvStore.Get<int>(CHARACTERS_BUCKET, $"{guid}.Awareness").Value,
                Charisma = _kvStore.Get<int>(CHARACTERS_BUCKET, $"{guid}.Charisma").Value,
                Education = _kvStore.Get<int>(CHARACTERS_BUCKET, $"{guid}.Education").Value,
                Resolve = _kvStore.Get<int>(CHARACTERS_BUCKET, $"{guid}.Resolve").Value,
                Body = _kvStore.Get<int>(CHARACTERS_BUCKET, $"{guid}.Body").Value,
                Inventory = _kvStore.Get<ItemData[]>(CHARACTERS_BUCKET, $"{guid}.Inventory").Value,
                Statistics = _kvStore.Get<Statistic[]>(CHARACTERS_BUCKET, $"{guid}.Statistics").Value
            };
            
            characterSaves.Add(new CharacterSave(character));
        }
        
        return characterSaves.ToArray();
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
            _kvStore.Put(CHARACTERS_BUCKET, $"{character.Guid}.Name", character.Name);
            _kvStore.Put(CHARACTERS_BUCKET, $"{character.Guid}.LastPlayedMs", character.LastPlayedMs);
            _kvStore.Put(CHARACTERS_BUCKET, $"{character.Guid}.AgeMs", character.AgeMs);
            _kvStore.Put(CHARACTERS_BUCKET, $"{character.Guid}.Strength", character.Strength);
            _kvStore.Put(CHARACTERS_BUCKET, $"{character.Guid}.Precision", character.Precision);
            _kvStore.Put(CHARACTERS_BUCKET, $"{character.Guid}.Awareness", character.Awareness);
            _kvStore.Put(CHARACTERS_BUCKET, $"{character.Guid}.Charisma", character.Charisma);
            _kvStore.Put(CHARACTERS_BUCKET, $"{character.Guid}.Education", character.Education);
            _kvStore.Put(CHARACTERS_BUCKET, $"{character.Guid}.Resolve", character.Resolve);
            _kvStore.Put(CHARACTERS_BUCKET, $"{character.Guid}.Body", character.Body);
            _kvStore.Put(CHARACTERS_BUCKET, $"{character.Guid}.Inventory", character.Inventory);
            _kvStore.Put(CHARACTERS_BUCKET, $"{character.Guid}.Statistics", character.Statistics);

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
