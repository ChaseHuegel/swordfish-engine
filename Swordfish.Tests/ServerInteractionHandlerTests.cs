using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Interactions;
using WaywardBeyond.Voxels;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Pins the default handlers the server composition registers: the content selection for a held item and
/// the authority write that clears or fills a voxel.
/// </summary>
public class ServerInteractionHandlerTests
{
    [Fact]
    public void BreakClearsTheVoxelAndRebuildsTheCollider()
    {
        var store = new DataStore();
        VoxelObject voxels = FilledVoxels();
        int entity = AllocStructure(store, voxels);
        var handler = new VoxelWriteCommitter(new ServerPeers(), new FakeRegistry(), NullLogger<VoxelWriteCommitter>.Instance);

        InteractionContext context = Context(store, entity);
        context.Resolution = new InteractionResolution(InteractionAction.Break, entity, new Int3(0, 0, 0), voxels.Get(0, 0, 0));

        Assert.Equal(InteractionDecision.Handled, handler.Handle(ref context));
        Assert.Equal(0, voxels.Get(0, 0, 0).ID);
        Assert.True(store.TryGet(entity, out ColliderComponent _), "The write must rebuild the collider.");
    }

    [Fact]
    public void PlaceWritesTheResolvedVoxel()
    {
        var store = new DataStore();
        VoxelObject voxels = FilledVoxels();
        int entity = AllocStructure(store, voxels);
        var handler = new VoxelWriteCommitter(new ServerPeers(), new FakeRegistry(), NullLogger<VoxelWriteCommitter>.Instance);

        InteractionContext context = Context(store, entity);
        var placed = new Voxel(2, new ShapeLight(BrickShape.Block, lightLevel: 0), default);
        context.Resolution = new InteractionResolution(InteractionAction.Place, entity, new Int3(1, 0, 0), placed);

        Assert.Equal(InteractionDecision.Handled, handler.Handle(ref context));
        Assert.Equal(2, voxels.Get(1, 0, 0).ID);
    }

    [Fact]
    public void AnUnresolvedInteractionWritesNothing()
    {
        var store = new DataStore();
        VoxelObject voxels = FilledVoxels();
        int entity = AllocStructure(store, voxels);
        var handler = new VoxelWriteCommitter(new ServerPeers(), new FakeRegistry(), NullLogger<VoxelWriteCommitter>.Instance);

        InteractionContext context = Context(store, entity);

        Assert.Equal(InteractionDecision.Continue, handler.Handle(ref context));
        Assert.Equal(1, voxels.Get(0, 0, 0).ID);
    }

    [Fact]
    public void TheItemHandlerSelectsThePlacedBrick()
    {
        var (items, bricks) = LoadContent();
        var handler = new ItemPlaceableSelector(items, bricks);
        var store = new DataStore();

        InteractionContext context = Context(store, entity: 0, heldItemID: "wb:rock");

        Assert.Equal(InteractionDecision.Handled, handler.Handle(ref context));
        Assert.Equal("wb:rock", context.Placeable?.ID);
    }

    [Fact]
    public void TheItemHandlerLeavesAnUnknownItemForLaterHandlers()
    {
        var (items, bricks) = LoadContent();
        var handler = new ItemPlaceableSelector(items, bricks);
        var store = new DataStore();

        InteractionContext context = Context(store, entity: 0, heldItemID: "wb:missing");

        Assert.Equal(InteractionDecision.Continue, handler.Handle(ref context));
        Assert.Null(context.Placeable);
    }

    private static InteractionContext Context(DataStore store, int entity, string? heldItemID = null)
    {
        var request = new InteractionRequest(
            default,
            new BrickInteraction
            {
                TargetEntity = Uuid.FromValue(1).ToValue(),
                TargetX = 0,
                TargetY = 0,
                TargetZ = 0,
                HintShape = 0,
                HintOrientation = 0,
            },
            InteractionKind.PrimaryPressed,
            null,
            GameMode.Adventure,
            reach: 9.5f
        );

        return new InteractionContext(
            store,
            player: entity,
            Uuid.FromValue(1),
            sequence: 1,
            world: null!,
            in request,
            heldItemID
        );
    }

    private static int AllocStructure(DataStore store, VoxelObject voxels)
    {
        int entity = store.Alloc(Uuid.FromValue(1));
        store.AddOrUpdate(entity, new TransformComponent());
        store.AddOrUpdate(entity, new VoxelWorldComponent(voxels));
        return entity;
    }

    private static VoxelObject FilledVoxels()
    {
        var voxels = new VoxelObject(4);
        voxels.Set(0, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Block, lightLevel: 0), default));
        return voxels;
    }

    private static (IItemDatabase Items, IBrickDatabase Bricks) LoadContent()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Bricks", "assets")));
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Items", "assets")));

        IFileParser[] parsers =
        [
            new TomlParser<BrickDefinitions>(),
            new TomlParser<ItemDefinitions>(),
        ];
        var parser = new VirtualFileParseService(parsers, vfs);

        var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs);
        var items = new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);
        return (items, bricks);
    }

    private sealed class FakeRegistry : IBrickRegistry
    {
        public ushort Id(string name) => name == "wb:rock" ? (ushort)1 : (ushort)0;

        public string? Name(ushort id) => id switch
        {
            1 => "wb:rock",
            2 => "wb:panel",
            _ => null,
        };
    }
}
