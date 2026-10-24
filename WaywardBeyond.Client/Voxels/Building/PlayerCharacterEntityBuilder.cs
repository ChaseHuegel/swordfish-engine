using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;

namespace WaywardBeyond.Client.Voxels.Building;

internal sealed class PlayerCharacterEntityBuilder(in IRenderContext renderContext)
{
    private readonly IRenderContext _renderContext = renderContext;

    public void Decorate(Entity player, Character character, Vector3 spawnPosition, Quaternion spawnOrientation)
    {
        player.Add<PlayerComponent>();
        player.AddOrUpdate(new PendingInventoryComponent());
        player.AddOrUpdate(new EquipmentComponent(character.ActiveInventorySlot));
        player.AddOrUpdate(new IdentifierComponent(character.Name, "game"));
        player.AddOrUpdate(new TransformComponent(
            spawnPosition,
            spawnOrientation,
            CharacterDefaults.PlayerScale
        ));
        player.AddOrUpdate(new InputComponent());
        player.AddOrUpdate(PendingInputComponent.Create());
        player.AddOrUpdate(CharacterDefaults.CreateCharacterPhysicsComponent());
        player.AddOrUpdate(CharacterDefaults.CreateCharacterColliderComponent(CharacterDefaults.PlayerScale));
        
        player.AddOrUpdate(new CharacterComponent(character));
        player.AddOrUpdate(new GameModeComponent(character.GameMode));

        //  The client is not the authority for starter inventory; the server grants the starter
        //  loadout as part of the seeded interaction context and echoes it down through reconcile.
        //  The local build carries only what the saved character had (if anything) so the pre-echo
        //  presentation matches the seed, and the authoritative component replaces it once Playing.
        var inventory = new InventoryComponent();
        if (character.Inventory != null)
        {
            for (var i = 0; i < character.Inventory.Length; i++)
            {
                ItemData itemData = character.Inventory[i];

                if (i < inventory.Contents.Length)
                {
                    //  In-place array write; the AddOrUpdate below auto-marks the component dirty
                    inventory.Contents[i] = itemData;
                }
                else
                {
                    inventory.Add(itemData);
                }
            }
        }

        player.AddOrUpdate(inventory);

        //  Child the camera to the player for a first person view
        var cameraChildComponent = new ChildComponent(player);
        _renderContext.MainCamera.Get().Entity.AddOrUpdate(cameraChildComponent);
    }
}