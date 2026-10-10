using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Data;
using WaywardBeyond.Networking.Components;

namespace WaywardBeyond.Client.Saves;

internal sealed class CharacterSaveManager(
    ILogger<CharacterSaveManager> logger,
    ICharacterStorage characterStorage,
    ActiveCharacterSave activeCharacterSave
) {
    public Character? ActiveSave
    {
        get => activeCharacterSave.ActiveSave;
        set => activeCharacterSave.ActiveSave = value;
    }

    public Result<Character> Load()
    {
        if (ActiveSave == null)
        {
            return Result<Character>.FromFailure("No character selected");
        }
        
        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Character character = ActiveSave.Value with
        {
            LastPlayedMs = nowUtcMs,
        };
        
        ActiveSave = character;
        
        return Result<Character>.FromSuccess(character);
    }

    public void Save(DataStore store)
    {
        if (WaywardBeyond.GameState < GameState.Playing)
        {
            return;
        }

        if (ActiveSave == null)
        {
            return;
        }

        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Character character = ActiveSave.Value with
        {
            AgeMs = SaveTime.Accumulate(ActiveSave.Value.AgeMs, ActiveSave.Value.LastPlayedMs, nowUtcMs),
            LastPlayedMs = nowUtcMs,
        };

        //  Fetch the character's inventory data
        store.Query<CharacterComponent, TransformComponent>(0f, UpdateInventory);
        void UpdateInventory(float delta, DataStore store, int entity, in CharacterComponent characterComponent, in TransformComponent transform)
        {
            //  The local player entity carries the server mirror uuid, not the character's own uuid, so
            //  match through the Character the component holds rather than the entity uuid.
            if (characterComponent.Character.Uuid != character.Uuid)
            {
                return;
            }

            if (!store.TryGet(entity, out InventoryComponent inventoryComponent))
            {
                return;
            }

            character.Inventory = new ItemData[inventoryComponent.Contents.Length];
            for (var i = 0; i < inventoryComponent.Contents.Length; i++)
            {
                character.Inventory[i] = inventoryComponent.Contents[i];
            }

            if (store.TryGet(entity, out EquipmentComponent equipment))
            {
                character.ActiveInventorySlot = equipment.ActiveInventorySlot;
            }

            if (store.TryGet(entity, out GameModeComponent gameMode))
            {
                character.GameMode = gameMode.Mode;
            }
        }

        Result saveResult = characterStorage.SaveCharacter(character);
        if (saveResult)
        {
            ActiveSave = character;
        }
        else
        {
            logger.LogError(saveResult.Exception, "Failed to save character {Name} ({Id}): {Message}", character.Name, character.Uuid, saveResult.Message);
        }
    }
    
    public void Delete(Character character)
    {
        if (!characterStorage.DeleteCharacter(character.Uuid))
        {
            logger.LogError("Failed to delete character {Name} ({Id})", character.Name, character.Uuid);
        }
    }
    
    internal Character? GetMostRecentSave()
    {
        return characterStorage.GetAllCharacters()
            .OrderByDescending(c => c.LastPlayedMs)
            .FirstOrDefault();
    }
}
