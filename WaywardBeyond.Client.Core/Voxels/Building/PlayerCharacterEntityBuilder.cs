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
        
        var inventory = new InventoryComponent(size: 45);
        if (character.Inventory == null)
        {
            inventory.Add(InventoryComponent.Stack("laser", 1, 1));
            inventory.Add(InventoryComponent.Stack("panel", 100, 100));
            inventory.Add(InventoryComponent.Stack("thruster", 100, 100));
            inventory.Add(InventoryComponent.Stack("display_control", 100, 100));
            inventory.Add(InventoryComponent.Stack("caution_panel", 100, 100));
            inventory.Add(InventoryComponent.Stack("glass", 100, 100));
            inventory.Add(InventoryComponent.Stack("display_monitor", 100, 100));
            inventory.Add(InventoryComponent.Stack("storage", 100, 100));
            inventory.Add(InventoryComponent.Stack("truss", 100, 100));
            inventory.Add(InventoryComponent.Stack("small_light", 100, 100));
            inventory.Add(InventoryComponent.Stack("light", 100, 100));
            inventory.Add(InventoryComponent.Stack("display_console", 100, 100));
            inventory.Add(InventoryComponent.Stack("ice", 100, 100));
            inventory.Add(InventoryComponent.Stack("rock", 100, 100));
            inventory.Add(InventoryComponent.Stack("control_buttons", 100, 100));
            inventory.Add(InventoryComponent.Stack("grate", 100, 100));
            inventory.Add(InventoryComponent.Stack("core", 100, 100));
            inventory.Add(InventoryComponent.Stack("porthole", 100, 100));
            inventory.Add(InventoryComponent.Stack("vent", 100, 100));
            inventory.Add(InventoryComponent.Stack("control_panel", 100, 100));
        }
        else
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