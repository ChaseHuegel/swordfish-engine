using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Client.Core.Voxels.Building;

internal sealed class PlayerCharacterEntityBuilder(in IRenderContext renderContext)
{
    private readonly IRenderContext _renderContext = renderContext;

    public void Decorate(Entity player, Character character)
    {
        player.Add<PlayerComponent>();
        player.AddOrUpdate(new EquipmentComponent(character.ActiveInventorySlot));
        player.AddOrUpdate(new IdentifierComponent(character.Name, PlayerBodyConfig.PLAYER_TAG));
        player.AddOrUpdate(new TransformComponent(
            PlayerBodyConfig.DEFAULT_SPAWN_POSITION,
            Quaternion.Identity,
            PlayerBodyConfig.PLAYER_SCALE
        ));
        player.AddOrUpdate(PlayerBodyConfig.CreatePhysics());
        player.AddOrUpdate(PlayerBodyConfig.CreateCollider(PlayerBodyConfig.PLAYER_SCALE));
        
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