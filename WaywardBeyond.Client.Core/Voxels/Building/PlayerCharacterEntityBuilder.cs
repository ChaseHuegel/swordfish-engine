using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;

namespace WaywardBeyond.Client.Core.Voxels.Building;

internal sealed class PlayerCharacterEntityBuilder(in IRenderContext renderContext)
{
    private readonly IRenderContext _renderContext = renderContext;

    public void Decorate(Entity player, Character character)
    {
        player.Add<PlayerComponent>();
        player.Add<EquipmentComponent>();
        player.AddOrUpdate(new IdentifierComponent(character.Name, "player"));
        player.AddOrUpdate(new TransformComponent(
            PlayerBodyConfig.DEFAULT_SPAWN_POSITION,
            Quaternion.Identity,
            PlayerBodyConfig.PLAYER_SCALE
        ));
        player.AddOrUpdate(PlayerBodyConfig.CreatePhysics());
        player.AddOrUpdate(PlayerBodyConfig.CreateCollider(PlayerBodyConfig.PLAYER_SCALE));
        
        player.AddOrUpdate(new CharacterComponent(character));
        player.AddOrUpdate(new GameModeComponent(GameMode.Creative));
        
        var inventory = new InventoryComponent(size: 45);
        if (character.Inventory == null)
        {
            inventory.Add(new ItemStack("laser", count: 1, maxSize: 1));
            inventory.Add(new ItemStack("panel", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("thruster", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("display_control", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("caution_panel", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("glass", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("display_monitor", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("storage", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("truss", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("small_light", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("light", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("display_console", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("ice", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("rock", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("control_buttons", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("grate", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("core", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("porthole", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("vent", count: 100, maxSize: 100));
            inventory.Add(new ItemStack("control_panel", count: 100, maxSize: 100));
        }
        else
        {
            for (var i = 0; i < character.Inventory.Length; i++)
            {
                ItemData itemData = character.Inventory[i];
                var itemStack = new ItemStack(itemData.ID, itemData.Count, itemData.MaxSize);
                
                if (i < inventory.Contents.Length)
                {
                    //  In-place array write; the AddOrUpdate below auto-marks the component dirty
                    inventory.Contents[i] = itemStack;
                }
                else
                {
                    inventory.Add(itemStack);
                }
            }
        }

        player.AddOrUpdate(inventory);

        //  Child the camera to the player for a first person view
        var cameraChildComponent = new ChildComponent(player);
        _renderContext.MainCamera.Get().Entity.AddOrUpdate(cameraChildComponent);
    }
}