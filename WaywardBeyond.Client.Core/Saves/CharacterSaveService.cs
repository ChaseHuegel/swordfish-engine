using System;
using System.IO;
using Swordfish.ECS;
using Swordfish.Library.IO;
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
        Result<Character[]> getResult = _kvStore.GetAll<Character>(CHARACTERS_BUCKET);
        if (!getResult.Success)
        {
            return [];
        }

        Character[] characters = getResult.Value;
        var characterSaves = new CharacterSave[characters.Length];
        
        for (var i = 0; i < characters.Length; i++)
        {
            characterSaves[i] = new CharacterSave(characters[i]);
        }
        
        return characterSaves;
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
            
            //  Save the character
            Result putResult = _kvStore.Put(CHARACTERS_BUCKET, character.Guid, character);
            if (!putResult.Success)
            {
                _notificationService.Push(_localizedFormatter.GetString("notification.character.saving.failed", save.Character.Name));
                return new Result<CharacterSave>(success: false, default, putResult.Message, putResult.Exception);
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
