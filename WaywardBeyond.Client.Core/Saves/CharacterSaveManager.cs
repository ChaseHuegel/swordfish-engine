using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Client.Core.Saves;

internal sealed class CharacterSaveManager
{
    public Character? ActiveSave
    {
        get => _activeCharacterSave.ActiveSave;
        set => _activeCharacterSave.ActiveSave = value;
    }

    private readonly ILogger<CharacterSaveManager> _logger;
    private readonly ICharacterStorage _characterStorage;
    private readonly ActiveCharacterSave _activeCharacterSave;
    private readonly IECSContext _ecs;

    public CharacterSaveManager(
        ILogger<CharacterSaveManager> logger,
        ICharacterStorage characterStorage,
        ActiveCharacterSave activeCharacterSave,
        IECSContext ecs
    ) {
        _logger = logger;
        _characterStorage = characterStorage;
        _activeCharacterSave = activeCharacterSave;
        _ecs = ecs;

        //  Default to the most recent character save, if there is one
        ActiveSave = GetMostRecentSave();
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

    public void Save()
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
            AgeMs = ActiveSave.Value.AgeMs + (nowUtcMs - ActiveSave.Value.LastPlayedMs),
            LastPlayedMs = nowUtcMs,
        };

        //  Fetch the character's inventory data
        _ecs.World.DataStore.Query<CharacterComponent, TransformComponent>(0f, UpdateInventory);
        void UpdateInventory(float delta, DataStore store, int entity, in CharacterComponent characterComponent, in TransformComponent transform)
        {
            //  The local player entity carries the server mirror uuid, not the character's own id, so
            //  match through the Character the component holds rather than the entity uuid.
            if (characterComponent.Character.Id != character.Id)
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

        Result saveResult = _characterStorage.SaveCharacter(character);
        if (saveResult)
        {
            ActiveSave = character;
        }
        else
        {
            _logger.LogError(saveResult.Exception, "Failed to save character {Name} ({Id}): {Message}", character.Name, character.Id, saveResult.Message);
        }
    }
    
    public void Delete(Character character)
    {
        if (!_characterStorage.DeleteCharacter(character.Id))
        {
            _logger.LogError("Failed to delete character {Name} ({Id})", character.Name, character.Id);
        }
    }
    
    internal Character? GetMostRecentSave()
    {
        return _characterStorage.GetAllCharacters()
            .OrderByDescending(c => c.LastPlayedMs)
            .FirstOrDefault();
    }
}
