using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Statistics;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Client.Voxels.Building;

internal sealed class PlayerCharacterEntityBuilder(
    in IRenderContext renderContext,
    ISkillDatabase? skillDatabase = null,
    ISlotGroupSchemaDatabase? schemas = null
)
{
    private readonly IRenderContext _renderContext = renderContext;
    private readonly ISkillDatabase? _skillDatabase = skillDatabase;
    private readonly ISlotGroupSchemaDatabase? _schemas = schemas;

    public void Decorate(Entity player, Character character, Vector3 spawnPosition, Quaternion spawnOrientation)
    {
        var inventory = InventoryComponent.CreatePlayer(_schemas);
        if (character.KeyedInventory != null)
        {
            inventory.LoadSparse(character.KeyedInventory);
        }
        else if (character.Inventory != null)
        {
            KeyedSlotEntry[] migrated = KeyedInventoryMigration.FromLegacySlots(character.Inventory) ?? [];
            inventory.LoadSparse(migrated);
        }

        player.Add<PlayerComponent>();
        player.AddOrUpdate(new PendingInventoryComponent());
        player.AddOrUpdate(new EquipmentComponent(inventory.ClampHotbarSlot(character.ActiveInventorySlot)));
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
        player.AddOrUpdate(CharacterStatisticsSeeder.CreateSkillState(character.Statistics, _skillDatabase));
        player.AddOrUpdate(CharacterStatisticsSeeder.CreateStatistics(character.Statistics, _skillDatabase));

        //  The client is not the authority for starter inventory; the server grants the starter
        //  loadout as part of the seeded interaction context and echoes it down through reconcile.
        //  The local build carries only what the saved character had (if anything) so the pre-echo
        //  presentation matches the seed, and the authoritative component replaces it once Playing.
        player.AddOrUpdate(inventory);

        //  Child the camera to the player for a first person view
        var cameraChildComponent = new ChildComponent(player);
        _renderContext.MainCamera.Get().Entity.AddOrUpdate(cameraChildComponent);
    }
}
