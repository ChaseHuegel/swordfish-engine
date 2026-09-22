using Swordfish.ECS;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using Xunit;

namespace Swordfish.Tests;

public class InteractionContextComponentTests
{
    [Fact]
    public void ContextComponentsAutoRegisterAsServerOwned()
    {
        NetworkRegistry.Initialize([typeof(EquipmentComponent).Assembly]);

        Assert.True(NetworkRegistry.TryGetInfo<EquipmentComponent>(out NetworkComponentInfo equipment));
        Assert.Equal(12u, equipment.Uuid.ToValue());
        Assert.Equal(NetworkDirection.ServerOwned, equipment.Direction);
        Assert.IsType<NsdComponentCodec<EquipmentComponent>>(equipment.Codec);

        Assert.True(NetworkRegistry.TryGetInfo<InventoryComponent>(out NetworkComponentInfo inventory));
        Assert.Equal(13u, inventory.Uuid.ToValue());
        Assert.Equal(NetworkDirection.ServerOwned, inventory.Direction);
        Assert.IsType<NsdComponentCodec<InventoryComponent>>(inventory.Codec);

        Assert.True(NetworkRegistry.TryGetInfo<GameModeComponent>(out NetworkComponentInfo gameMode));
        Assert.Equal(14u, gameMode.Uuid.ToValue());
        Assert.Equal(NetworkDirection.ServerOwned, gameMode.Direction);
        Assert.IsType<NsdComponentCodec<GameModeComponent>>(gameMode.Codec);
    }

    [Fact]
    public void EquipmentComponentCodecRoundTrips()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new EquipmentComponent(6));

        var codec = new NsdComponentCodec<EquipmentComponent>();
        byte[] payload = codec.Serialize(store, entity);
        Assert.NotEmpty(payload);

        var output = new DataStore();
        int outputEntity = output.Alloc();
        codec.Apply(output, outputEntity, payload);

        Assert.True(output.TryGet(outputEntity, out EquipmentComponent result));
        Assert.Equal(6, result.ActiveInventorySlot);
    }

    [Fact]
    public void GameModeComponentCodecRoundTrips()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new GameModeComponent(GameMode.Adventure));

        var codec = new NsdComponentCodec<GameModeComponent>();
        byte[] payload = codec.Serialize(store, entity);
        Assert.NotEmpty(payload);

        var output = new DataStore();
        int outputEntity = output.Alloc();
        codec.Apply(output, outputEntity, payload);

        Assert.True(output.TryGet(outputEntity, out GameModeComponent result));
        Assert.Equal(GameMode.Adventure, result.Mode);
    }

    [Fact]
    public void InventoryComponentCodecRoundTripsContents()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new InventoryComponent(new ItemData[]
        {
            InventoryComponent.Stack("panel", 64, 100),
            InventoryComponent.Stack("laser", 1, 1),
        }));

        var codec = new NsdComponentCodec<InventoryComponent>();
        byte[] payload = codec.Serialize(store, entity);
        Assert.NotEmpty(payload);

        var output = new DataStore();
        int outputEntity = output.Alloc();
        codec.Apply(output, outputEntity, payload);

        Assert.True(output.TryGet(outputEntity, out InventoryComponent result));
        Assert.Equal(2, result.Contents.Length);
        Assert.Equal("panel", result.Contents[0].ID);
        Assert.Equal(64, result.Contents[0].Count);
        Assert.Equal("laser", result.Contents[1].ID);
    }

    [Fact]
    public void CharacterSeedRoundTrips()
    {
        var seed = new CharacterSeed
        {
            CharacterId = 42,
            Name = "Ada",
            Body = 3,
            InventoryContents =
            [
                InventoryComponent.Stack("rock", 5, 100),
            ],
            ActiveInventorySlot = 2,
            GameMode = (int)GameMode.Adventure,
        };

        byte[] bytes = seed.Serialize();
        CharacterSeed roundTripped = CharacterSeed.Deserialize(bytes);

        Assert.Equal(seed.CharacterId, roundTripped.CharacterId);
        Assert.Equal(seed.Name, roundTripped.Name);
        Assert.Equal(seed.Body, roundTripped.Body);
        Assert.Equal(seed.ActiveInventorySlot, roundTripped.ActiveInventorySlot);
        Assert.Equal(seed.GameMode, roundTripped.GameMode);
        Assert.Single(roundTripped.InventoryContents);
        Assert.Equal("rock", roundTripped.InventoryContents[0].ID);
        Assert.Equal(5, roundTripped.InventoryContents[0].Count);
    }

    [Fact]
    public void CharacterSeedAbsentFieldDefaultsToPrivateSeed()
    {
        //  A JoinRequest carrying only the identity fields deserializes a default seed (as an older
        //  client would emit): empty inventory, slot 0, creative.
        var join = new JoinRequest
        {
            LevelGuid = "level",
            CharacterId = 42,
            PublicView = new PublicView { CharacterId = 42, Name = "Ada", Body = 3 },
        };

        byte[] bytes = join.Serialize();
        JoinRequest roundTripped = JoinRequest.Deserialize(bytes);

        Assert.NotNull(roundTripped.Seed);
        Assert.Null(roundTripped.Seed.InventoryContents);
        Assert.Equal(0, roundTripped.Seed.ActiveInventorySlot);
        Assert.Equal((int)GameMode.Creative, roundTripped.Seed.GameMode);
    }

    [Fact]
    public void InventoryDefaultInstanceIsCanonicalSize()
    {
        var inventory = new InventoryComponent();
        Assert.Equal(InventoryComponent.DefaultSlotCount, inventory.Contents.Length);
    }

    [Fact]
    public void CopyFromBoundsToCapacityAndKeepsFullSizedArray()
    {
        var inventory = new InventoryComponent(4);
        inventory.CopyFrom(new ItemData[]
        {
            InventoryComponent.Stack("a", 1, 10),
            InventoryComponent.Stack("b", 2, 10),
            InventoryComponent.Stack("c", 3, 10),
            InventoryComponent.Stack("d", 4, 10),
            InventoryComponent.Stack("e", 5, 10),
            InventoryComponent.Stack("f", 6, 10),
        });

        //  Contents is bounded to the canonical array size, never truncated below it.
        Assert.Equal(4, inventory.Contents.Length);
        Assert.Equal("a", inventory.Contents[0].ID);
        Assert.Equal("b", inventory.Contents[1].ID);
        Assert.Equal("c", inventory.Contents[2].ID);
        Assert.Equal("d", inventory.Contents[3].ID);
    }

    [Fact]
    public void CopyFromEmptySeedYieldsCanonicalSizedInventory()
    {
        var inventory = new InventoryComponent();
        inventory.CopyFrom(null);

        //  The replicated inventory is always full-size so any active slot is in bounds.
        Assert.Equal(InventoryComponent.DefaultSlotCount, inventory.Contents.Length);
        Assert.All(inventory.Contents, stack => Assert.Null(stack.ID));
    }

    [Fact]
    public void ClampSlotBoundsIntoCanonicalRange()
    {
        Assert.Equal(0, InventoryComponent.ClampSlot(-5));
        Assert.Equal(0, InventoryComponent.ClampSlot(0));
        Assert.Equal(InventoryComponent.DefaultSlotCount - 1, InventoryComponent.ClampSlot(InventoryComponent.DefaultSlotCount + 20));
    }

    [Fact]
    public void GameModeClampAndEquipmentSeedValuesAreCanonical()
    {
        //  Guards the seed path invariants: the canonical inventory size equals the default slot count
        //  the client builds, so a full-array seed + clamped slot can never index out of bounds.
        Assert.Equal(45, InventoryComponent.DefaultSlotCount);
        Assert.InRange(InventoryComponent.ClampSlot(99), 0, InventoryComponent.DefaultSlotCount - 1);
    }
}