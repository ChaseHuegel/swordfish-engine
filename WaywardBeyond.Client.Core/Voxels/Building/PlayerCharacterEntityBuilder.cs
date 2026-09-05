using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.Voxels.Models;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Voxels.Building;

internal sealed class PlayerCharacterEntityBuilder(in DataStore dataStore, in IRenderContext renderContext)
{
    private readonly DataStore _dataStore = dataStore;
    private readonly IRenderContext _renderContext = renderContext;

    public Entity Create(Character character, CharacterEntityModel model)
    {
        int ptr = _dataStore.Alloc(model.Uuid);
        var player = new Entity(ptr, _dataStore);
        Decorate(player, character, model);
        return player;
    }

    public void Decorate(Entity player, Character character, CharacterEntityModel model)
    {
        player.Add<PlayerComponent>();
        player.Add<EquipmentComponent>();
        player.AddOrUpdate(new IdentifierComponent(character.Name, "player"));
        player.AddOrUpdate(new TransformComponent(model.Position, model.Orientation, model.Scale));
        player.AddOrUpdate(new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));

        const float playerStandingHeight = 1.7f;
        const float playerStandingOffset = -0.75f;
        const float playerFlyingHeight = 0.75f;
        const float playerFlyingOffset = -0.2f;
        var playerCapsule = new Shape(new Box3(new Vector3(0.25f, playerFlyingHeight, 0.25f) * model.Scale));
        var playerCollider = new CompoundShape([playerCapsule], [new Vector3(0f, playerFlyingOffset, 0f) * model.Scale], [Quaternion.Identity]);
        player.AddOrUpdate(new ColliderComponent(playerCollider));
        
        player.AddOrUpdate(new CharacterComponent(character));
        player.AddOrUpdate(new GameModeComponent(model.GameMode));
        
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