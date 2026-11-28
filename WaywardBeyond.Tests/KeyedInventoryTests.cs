#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using DryIoc;
using Krill;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using Swordfish.Tests;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Data.Migrations;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Components;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Items;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Permissions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Systems;
using Xunit;
using Version = WaywardBeyond.Data.Version;

namespace WaywardBeyond.Tests;

/// <summary>Covers keyed slot layouts, sparse replication, legacy migration, orphan recovery, filters, and transfer security.</summary>
public class KeyedInventoryTests
{
    private static readonly SlotId Hotbar0 = new(SlotGroups.HOTBAR, 0);
    private static readonly SlotId Hotbar2 = new(SlotGroups.HOTBAR, 2);
    private static readonly SlotId Backpack0 = new(SlotGroups.BACKPACK, 0);
    private static readonly SlotId Backpack4 = new(SlotGroups.BACKPACK, 4);
    private static readonly SlotId Wearable0 = new(SlotGroups.WEARABLES, 0);
    private static readonly SlotId Wearable3 = new(SlotGroups.WEARABLES, 3);
    private static readonly SlotId Augment0 = new(SlotGroups.AUGMENTS, 0);
    private static readonly SlotId Augment1 = new(SlotGroups.AUGMENTS, 1);

    [Fact]
    public void ResizeGroupPreservesAdjacentGroupSlotAddresses()
    {
        InventoryComponent inventory = InventoryComponent.Create(
            new SlotGroupDescriptor(SlotGroups.HOTBAR, 9),
            new SlotGroupDescriptor(SlotGroups.BACKPACK, 6),
            new SlotGroupDescriptor(SlotGroups.WEARABLES, 6),
            new SlotGroupDescriptor(SlotGroups.AUGMENTS, 5));

        inventory.Set(in Hotbar2, new ItemData("wb:pickaxe", 1, 1));
        inventory.Set(in Backpack4, new ItemData("wb:rock", 12, 64));
        inventory.Set(in Wearable3, new ItemData("wb:visor", 1, 1));
        inventory.Set(in Augment1, new ItemData("wb:booster", 1, 1));

        Assert.True(inventory.ResizeGroup(SlotGroups.BACKPACK, 27));
        Assert.Equal(27, inventory.GetGroupCapacity(SlotGroups.BACKPACK));
        Assert.Equal("wb:pickaxe", inventory.Get(in Hotbar2).ID);
        Assert.Equal("wb:rock", inventory.Get(in Backpack4).ID);
        Assert.Equal(12, inventory.Get(in Backpack4).Count);
        Assert.Equal("wb:visor", inventory.Get(in Wearable3).ID);
        Assert.Equal("wb:booster", inventory.Get(in Augment1).ID);

        //  Shrinking a custom group routes displaced items into free backpack slots without data loss.
        Assert.True(inventory.ResizeGroup(SlotGroups.CARGO, 4));
        var cargo3 = new SlotId(SlotGroups.CARGO, 3);
        inventory.Set(in cargo3, new ItemData("wb:panel", 8, 64));

        Assert.True(inventory.ResizeGroup(SlotGroups.CARGO, 2));
        Assert.Equal("wb:panel", inventory.Get(in Backpack0).ID);
        Assert.Equal(8, inventory.Get(in Backpack0).Count);
        Assert.True(inventory.Get(in cargo3).IsEmpty());
    }

    [Fact]
    public void SparseWireCodecRoundTripsAndHydratesWithoutEmptySlotPadding()
    {
        InventoryComponent original = InventoryComponent.CreatePlayer();
        original.Set(in Hotbar2, new ItemData("laser", 1, 1));
        original.Set(in Backpack4, new ItemData("wb:rock", 17, 64));
        original.Set(in Wearable0, new ItemData("wb:helmet", 1, 1));

        KeyedSlotEntry[] sparse = original.ToSparseEntries();
        Assert.Equal(3, sparse.Length);

        var codec = new InventoryCodec();
        byte[] payload = codec.Serialize(in original);

        var targetStore = new DataStore();
        int targetEntity = targetStore.Alloc();
        Result applied = codec.Apply(targetStore, targetEntity, payload);
        Assert.True(applied.Success, applied.Message);

        Assert.True(targetStore.TryGet(targetEntity, out InventoryComponent decoded));
        Assert.Equal(original.TotalCapacity, decoded.TotalCapacity);
        Assert.Equal("laser", decoded.Get(in Hotbar2).ID);
        Assert.Equal("wb:rock", decoded.Get(in Backpack4).ID);
        Assert.Equal(17, decoded.Get(in Backpack4).Count);
        Assert.Equal("wb:helmet", decoded.Get(in Wearable0).ID);
    }

    [Fact]
    public void SparseLoadRecoversOrphansAndPreservesOverflowNonDestructively()
    {
        InventoryComponent inventory = InventoryComponent.Create(
            new SlotGroupDescriptor(SlotGroups.HOTBAR, 1),
            new SlotGroupDescriptor(SlotGroups.BACKPACK, 1));

        KeyedSlotEntry[] entries =
        [
            new KeyedSlotEntry(SlotGroups.HOTBAR, 0, new ItemData("wb:rock", 5, 64)),
            new KeyedSlotEntry("wb:removed_mod_group", 0, new ItemData("wb:core", 2, 16)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 99, new ItemData("wb:panel", 4, 64)),
            new KeyedSlotEntry(SlotGroups.HOTBAR, 0, new ItemData("wb:ice", 3, 64)),
        ];

        inventory.LoadSparse(entries);

        //  wb:removed_mod_group[0] moves to the only free backpack slot; wb:panel and the duplicate wb:ice stay preserved as orphans.
        Assert.Equal("wb:rock", inventory.Get(in Hotbar0).ID);
        Assert.Equal("wb:core", inventory.Get(in Backpack0).ID);
        Assert.Equal(2, inventory.Orphans.Count);

        //  Expanding the backpack automatically drains the preserved orphans into the new capacity.
        Assert.True(inventory.ResizeGroup(SlotGroups.BACKPACK, 4));
        Assert.Empty(inventory.Orphans);
        Assert.Equal("wb:panel", inventory.Get(new SlotId(SlotGroups.BACKPACK, 1)).ID);
        Assert.Equal("wb:ice", inventory.Get(new SlotId(SlotGroups.BACKPACK, 2)).ID);
    }

    [Fact]
    public void OrphanedEntriesSurviveWireRoundTripWhenBackpackIsFull()
    {
        InventoryComponent inventory = InventoryComponent.Create(
            new SlotGroupDescriptor(SlotGroups.HOTBAR, 1),
            new SlotGroupDescriptor(SlotGroups.BACKPACK, 1));
        inventory.LoadSparse(
        [
            new KeyedSlotEntry(SlotGroups.HOTBAR, 0, new ItemData("wb:rock", 1, 64)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 0, new ItemData("wb:panel", 1, 64)),
            new KeyedSlotEntry("wb:legacy_extra", 7, new ItemData("wb:thruster", 3, 16)),
        ]);

        var codec = new InventoryCodec();
        byte[] bytes = codec.Serialize(in inventory);

        var store = new DataStore();
        int entity = store.Alloc();
        Assert.True(codec.Apply(store, entity, bytes).Success);

        Assert.True(store.TryGet(entity, out InventoryComponent hydrated));
        KeyedSlotEntry orphan = Assert.Single(hydrated.Orphans);
        Assert.Equal("wb:thruster", orphan.Item.ID);
        Assert.Equal(3, orphan.Item.Count);
        Assert.True(hydrated.ResizeGroup(SlotGroups.BACKPACK, 2));
        Assert.Empty(hydrated.Orphans);
        Assert.Equal("wb:thruster", hydrated.Get(new SlotId(SlotGroups.BACKPACK, 1)).ID);
    }

    [Fact]
    public void KeyedCharacterInventoryRoundTripsThroughSqliteStorage()
    {
        KeyedSlotEntry[] entries =
        [
            new KeyedSlotEntry(SlotGroups.HOTBAR, 0, new ItemData("laser", 1, 1)),
            new KeyedSlotEntry(SlotGroups.HOTBAR, 8, new ItemData("wb:torch", 12, 32)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 0, new ItemData("wb:rock", 64, 64)),
            new KeyedSlotEntry(SlotGroups.WEARABLES, 0, new ItemData("wb:helmet", 1, 1)),
            new KeyedSlotEntry(SlotGroups.AUGMENTS, 2, new ItemData("wb:speed_chip", 1, 1)),
        ];

        var character = new Character(
            new Version(DataVersion.CURRENT, "Wayward Beyond", "Save"),
            4242UL,
            new Playtime(),
            "Saved",
            string.Empty,
            0,
            GameMode.Adventure,
            null,
            entries);

        string tempDir = Path.Combine(Path.GetTempPath(), $"wb-inv-save-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var storageConfig = new StorageConfig();
            storageConfig.SaveRoot.Set(tempDir);
            var storagePaths = new StoragePaths(storageConfig, AppPaths.ForWorkingDirectory());
            var migrator = new Migrator([]);
            var storage = new SqliteCharacterStorage(NullLogger<SqliteCharacterStorage>.Instance, storagePaths, migrator);

            Assert.True(storage.SaveCharacter(character).Success);
            Result<Character> loaded = storage.GetCharacter(4242UL);
            Assert.True(loaded.Success, loaded.Message);

            Character restored = loaded.Value;
            Assert.Equal(DataVersion.CURRENT, restored.Version.DataVersion);
            Assert.NotNull(restored.Inventory);
            Assert.Equal(5, restored.Inventory!.Length);
            Assert.Contains(restored.Inventory, entry => entry.GroupId == SlotGroups.HOTBAR && entry.LocalIndex == 0 && entry.Item.ID == "laser");
            Assert.Contains(restored.Inventory, entry => entry.GroupId == SlotGroups.HOTBAR && entry.LocalIndex == 8 && entry.Item.ID == "wb:torch");
            Assert.Contains(restored.Inventory, entry => entry.GroupId == SlotGroups.BACKPACK && entry.LocalIndex == 0 && entry.Item.ID == "wb:rock");
            Assert.Contains(restored.Inventory, entry => entry.GroupId == SlotGroups.WEARABLES && entry.LocalIndex == 0 && entry.Item.ID == "wb:helmet");
            Assert.Contains(restored.Inventory, entry => entry.GroupId == SlotGroups.AUGMENTS && entry.LocalIndex == 2 && entry.Item.ID == "wb:speed_chip");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static SlotGroupSchemaDatabase LoadShippedSchemas()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine(AppContext.BaseDirectory, "TestFiles", "Items", "assets")));
        IFileParser[] parsers = [new TomlParser<SlotGroupDefinitions>()];
        var parseService = new VirtualFileParseService(parsers, vfs);
        return new SlotGroupSchemaDatabase(NullLogger<SlotGroupSchemaDatabase>.Instance, parseService, vfs);
    }

    [Fact]
    public void ShippedSlotGroupTomlAndFilterTagsEnforceGroupAndPerSlotRules()
    {
        SlotGroupSchemaDatabase schemas = LoadShippedSchemas();

        Result<SlotGroupSchema> wearableSchema = schemas.Get(SlotGroups.WEARABLES);
        Assert.True(wearableSchema.Success);
        Assert.False(wearableSchema.Value.AllowedTags is string[]);
        Assert.False(wearableSchema.Value.SlotAllowedTags[0] is string[]);

        var tags = new LocalizedTags(new Dictionary<string, List<string>>
        {
            [ItemTags.WEARABLE] = ["wb:helmet_alpha", "wb:boots_alpha"],
            [ItemTags.WEARABLE_HELMET] = ["wb:helmet_alpha"],
            [ItemTags.AUGMENT] = ["wb:speed_chip"],
        });
        var context = new InventoryValidationContext(schemas, tags);

        InventoryComponent inventory = InventoryComponent.CreatePlayer(schemas);
        inventory.Set(in Hotbar0, new ItemData("wb:rock", 5, 64));
        inventory.Set(in Backpack0, new ItemData("wb:helmet_alpha", 1, 1));
        inventory.Set(in Backpack4, new ItemData("wb:boots_alpha", 1, 1));
        inventory.Set(in Wearable3, new ItemData("wb:boots_alpha", 1, 1));

        //  Invalid: stone cannot go into wearable helmet slot.
        Assert.False(InventoryResolver.Apply(ref inventory, new TransferOp(0UL, Hotbar0, 0UL, Wearable0, TransferMode.Exact, null), in context));
        Assert.Equal(5, inventory.Get(in Hotbar0).Count);

        //  Invalid: boots cannot go into helmet slot [0].
        Assert.False(InventoryResolver.Apply(ref inventory, new TransferOp(0UL, Backpack4, 0UL, Wearable0, TransferMode.Exact, null), in context));

        //  Valid: helmet moves cleanly into wb:wearables[0].
        Assert.True(InventoryResolver.Apply(ref inventory, new TransferOp(0UL, Backpack0, 0UL, Wearable0, TransferMode.Exact, null), in context));
        Assert.Equal("wb:helmet_alpha", inventory.Get(in Wearable0).ID);

        //  Invalid swap: stone cannot enter Wearable0 in either direction of a swap with the equipped helmet.
        Assert.False(InventoryResolver.Apply(ref inventory, new TransferOp(0UL, Hotbar0, 0UL, Wearable0, TransferMode.Exact, null), in context));
        Assert.False(InventoryResolver.Apply(ref inventory, new TransferOp(0UL, Wearable0, 0UL, Hotbar0, TransferMode.Exact, null), in context));
        //  Move helmet out into empty Backpack0, then verify augment filter checks.
        Assert.True(InventoryResolver.Apply(ref inventory, new TransferOp(0UL, Wearable0, 0UL, Backpack0, TransferMode.Exact, null), in context));

        inventory.Set(in Backpack0, new ItemData("wb:speed_chip", 1, 1));
        Assert.True(InventoryResolver.Apply(ref inventory, new TransferOp(0UL, Backpack0, 0UL, Augment0, TransferMode.Exact, null), in context));
        Assert.Equal("wb:speed_chip", inventory.Get(in Augment0).ID);

        //  Swapping rock from Hotbar0 with speed_chip in Augment0 must fail bidirectionally because rock fails Augment0's filter.
        Assert.False(InventoryResolver.Apply(ref inventory, new TransferOp(0UL, Augment0, 0UL, Hotbar0, TransferMode.Exact, null), in context));
        Assert.Equal("wb:speed_chip", inventory.Get(in Augment0).ID);
        Assert.Equal("wb:rock", inventory.Get(in Hotbar0).ID);
    }

    [Fact]
    public void AutoStackSupportsExplicitFilteredGroupTarget()
    {
        SlotGroupSchemaDatabase schemas = LoadShippedSchemas();
        var tags = new LocalizedTags(new Dictionary<string, List<string>>
        {
            [ItemTags.WEARABLE] = ["wb:helmet_alpha"],
            [ItemTags.WEARABLE_HELMET] = ["wb:helmet_alpha"],
        });
        var context = new InventoryValidationContext(schemas, tags);

        InventoryComponent inventory = InventoryComponent.CreatePlayer(schemas);
        inventory.Set(in Backpack0, new ItemData("wb:helmet_alpha", 1, 1));

        //  Targeted AutoStack into wb:wearables places the helmet directly into matching slot [0].
        var op = new TransferOp(0UL, Backpack0, 0UL, new SlotId(SlotGroups.WEARABLES, -1), TransferMode.AutoStack, null);
        Assert.True(InventoryResolver.Apply(ref inventory, in op, in context));
        Assert.Equal("wb:helmet_alpha", inventory.Get(in Wearable0).ID);
        Assert.True(inventory.Get(in Backpack0).IsEmpty());
    }

    [Fact]
    public void ServerInventorySystemClampsStackCapacityAndPreventsSelfAliasDuplication()
    {
        var store = new DataStore();
        var itemDb = new StubItemDatabase(new Item("wb:compact_cell", "Cell", null, 4, null, null, null, null));
        var system = new ServerInventorySystem(itemDatabase: itemDb);

        int player = store.Alloc(Uuid.FromValue(1001UL));
        InventoryComponent inv = InventoryComponent.CreatePlayer();
        //  Simulate oversized stack entering transfer path.
        inv.Set(in Hotbar0, new ItemData("wb:compact_cell", 10, 99));
        store.AddOrUpdate(player, inv);

        NetworkComponent net = NetworkComponent.Create();
        //  Anti-dupe: aliasing container 0UL with explicit playerUuid must collapse to self-move (rejected same-slot).
        net.StagedInventoryOps!.Stage(1, new TransferOp(0UL, Hotbar0, 1001UL, Hotbar0, TransferMode.Exact, null));
        //  Valid: destination stack size clamps to authoritative MaxStack=4, leaving 6 behind.
        net.StagedInventoryOps!.Stage(2, new TransferOp(0UL, Hotbar0, 1001UL, Backpack0, TransferMode.Exact, null));
        store.AddOrUpdate(player, net);

        system.Tick(1f, store);

        Assert.True(store.TryGet(player, out InventoryComponent updated));
        ItemData dest = updated.Get(in Backpack0);
        ItemData source = updated.Get(in Hotbar0);
        Assert.Equal(4, dest.Count);
        Assert.Equal(4, dest.MaxSize);
        Assert.Equal(6, source.Count);
    }

    [Fact]
    public void ServerInventorySystemEnforcesCrossContainerReachViewerLockPermissionAndPlayerIsolation()
    {
        var store = new DataStore();
        var sessions = new SessionManager();
        var resolver = new PermissionResolver(PermissionPolicy.Create([]));
        var permissions = new UserPermissionService(resolver);
        var itemDb = new StubItemDatabase(new Item("wb:rock", "Rock", null, 64, null, null, null, null));
        var system = new ServerInventorySystem(SlotGroupSchemaDatabase.Default, null, itemDb, sessions, permissions);

        Uuid clientId = Uuid.FromValue(77UL);
        permissions.Bind(clientId, UserClaim.Anonymous, isHost: false);

        Uuid playerUuid = Uuid.FromValue(2001UL);
        int player = store.Alloc(playerUuid);
        store.AddOrUpdate(player, NetworkComponent.Create());
        store.AddOrUpdate(player, new OwnedCharacterComponent());
        store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        InventoryComponent playerInv = InventoryComponent.CreatePlayer();
        playerInv.Set(in Hotbar0, new ItemData("wb:rock", 10, 64));
        store.AddOrUpdate(player, playerInv);
        sessions.Register(store, player, clientId, new Session(1));

        //  Nearby unlocked chest succeeds cross-container transfer and AutoStack.
        Uuid chestUuid = Uuid.FromValue(3001UL);
        int chest = store.Alloc(chestUuid);
        store.AddOrUpdate(chest, new TransformComponent(new Vector3(3f, 0f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(chest, InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.CARGO, 9)));

        //  Locked chest owned by another user denies transfer.
        Uuid lockedUuid = Uuid.FromValue(3002UL);
        int lockedChest = store.Alloc(lockedUuid);
        store.AddOrUpdate(lockedChest, new TransformComponent(new Vector3(2f, 0f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(lockedChest, InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.CARGO, 9)));
        store.AddOrUpdate(lockedChest, new ContainerAccessComponent { Owner = Uuid.FromValue(9999UL), IsLocked = true });

        //  Exclusive-viewer chest open by another player denies transfer.
        Uuid viewedUuid = Uuid.FromValue(3003UL);
        int viewedChest = store.Alloc(viewedUuid);
        store.AddOrUpdate(viewedChest, new TransformComponent(new Vector3(2f, 0f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(viewedChest, InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.CARGO, 9)));
        store.AddOrUpdate(viewedChest, new ContainerAccessComponent { ActiveViewer = Uuid.FromValue(9999UL) });

        //  Out-of-reach chest denies transfer.
        Uuid farUuid = Uuid.FromValue(3004UL);
        int farChest = store.Alloc(farUuid);
        store.AddOrUpdate(farChest, new TransformComponent(new Vector3(50f, 0f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(farChest, InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.CARGO, 9)));

        //  Permission-gated chest denies anonymous non-local player.
        Uuid permUuid = Uuid.FromValue(3005UL);
        int permChest = store.Alloc(permUuid);
        store.AddOrUpdate(permChest, new TransformComponent(new Vector3(2f, 0f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(permChest, InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.CARGO, 9)));
        store.AddOrUpdate(permChest, new ContainerAccessComponent { RequiredPermission = "wb.chest.admin" });

        //  Another player's entity denies transfer completely.
        Uuid victimUuid = Uuid.FromValue(2002UL);
        int victim = store.Alloc(victimUuid);
        store.AddOrUpdate(victim, new OwnedCharacterComponent());
        store.AddOrUpdate(victim, new TransformComponent(new Vector3(1f, 0f, 0f), Quaternion.Identity, Vector3.One));
        InventoryComponent victimInv = InventoryComponent.CreatePlayer();
        victimInv.Set(in Hotbar0, new ItemData("wb:diamond", 5, 64));
        store.AddOrUpdate(victim, victimInv);

        Assert.True(store.TryGet(player, out NetworkComponent net));
        var cargo0 = new SlotId(SlotGroups.CARGO, 0);
        net.StagedInventoryOps!.Stage(1, new TransferOp(0UL, Hotbar0, lockedUuid.ToValue(), cargo0, TransferMode.Exact, 4));
        net.StagedInventoryOps!.Stage(2, new TransferOp(0UL, Hotbar0, viewedUuid.ToValue(), cargo0, TransferMode.Exact, 4));
        net.StagedInventoryOps!.Stage(3, new TransferOp(0UL, Hotbar0, farUuid.ToValue(), cargo0, TransferMode.Exact, 4));
        net.StagedInventoryOps!.Stage(4, new TransferOp(0UL, Hotbar0, permUuid.ToValue(), cargo0, TransferMode.Exact, 4));
        net.StagedInventoryOps!.Stage(5, new TransferOp(victimUuid.ToValue(), Hotbar0, 0UL, Backpack0, TransferMode.Exact, null));
        net.StagedInventoryOps!.Stage(6, new TransferOp(0UL, Hotbar0, chestUuid.ToValue(), cargo0, TransferMode.Exact, 4));
        net.StagedInventoryOps!.Stage(7, new TransferOp(chestUuid.ToValue(), cargo0, 0UL, default, TransferMode.AutoStack, null));

        system.Tick(1f, store);

        Assert.True(store.TryGet(lockedChest, out InventoryComponent lockedAfter));
        Assert.True(store.TryGet(viewedChest, out InventoryComponent viewedAfter));
        Assert.True(store.TryGet(farChest, out InventoryComponent farAfter));
        Assert.True(store.TryGet(permChest, out InventoryComponent permAfter));
        Assert.True(store.TryGet(victim, out InventoryComponent victimAfter));
        Assert.True(store.TryGet(chest, out InventoryComponent chestAfter));
        Assert.True(store.TryGet(player, out InventoryComponent playerAfter));

        Assert.Empty(lockedAfter.ToSparseEntries());
        Assert.Empty(viewedAfter.ToSparseEntries());
        Assert.Empty(farAfter.ToSparseEntries());
        Assert.Empty(permAfter.ToSparseEntries());
        Assert.Equal(5, victimAfter.Get(in Hotbar0).Count);
        //  Op 6 moved 4 rock into chest cargo0, then op 7 auto-stacked those 4 back into player hotbar0!
        Assert.True(chestAfter.Get(in cargo0).IsEmpty());
        Assert.Equal(10, playerAfter.Get(in Hotbar0).Count);

        //  Verify locked chest succeeds for owner and permission chest succeeds with host/admin claim.
        store.AddOrUpdate(lockedChest, new ContainerAccessComponent { Owner = playerUuid, IsLocked = true });
        permissions.Bind(clientId, UserClaim.Anonymous, isHost: true);
        net.StagedInventoryOps!.Stage(8, new TransferOp(0UL, Hotbar0, lockedUuid.ToValue(), cargo0, TransferMode.Exact, 3));
        net.StagedInventoryOps!.Stage(9, new TransferOp(0UL, Hotbar0, permUuid.ToValue(), cargo0, TransferMode.Exact, 2));
        system.Tick(1f, store);

        Assert.True(store.TryGet(lockedChest, out lockedAfter));
        Assert.True(store.TryGet(permChest, out permAfter));
        Assert.True(store.TryGet(player, out playerAfter));
        Assert.Equal(3, lockedAfter.Get(in cargo0).Count);
        Assert.Equal(2, permAfter.Get(in cargo0).Count);
        Assert.Equal(5, playerAfter.Get(in Hotbar0).Count);
    }

    [Fact]
    public void ClearingBackpackSlotDrainsOrphansIdempotentlyAcrossWireSync()
    {
        InventoryComponent server = InventoryComponent.Create(
            new SlotGroupDescriptor(SlotGroups.HOTBAR, 1),
            new SlotGroupDescriptor(SlotGroups.BACKPACK, 1));
        server.LoadSparse(
        [
            new KeyedSlotEntry(SlotGroups.HOTBAR, 0, new ItemData("wb:rock", 1, 64)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 0, new ItemData("wb:panel", 1, 64)),
            new KeyedSlotEntry("wb:removed_mod", 0, new ItemData("wb:thruster", 3, 16)),
        ]);

        Assert.Single(server.Orphans);

        //  Clearing the backpack slot drains the orphan on the server immediately so wire sync cannot diverge.
        Assert.True(server.Set(in Backpack0, default));
        Assert.Empty(server.Orphans);
        Assert.Equal("wb:thruster", server.Get(in Backpack0).ID);
        Assert.Equal(3, server.Get(in Backpack0).Count);

        var codec = new InventoryCodec();
        byte[] payload = codec.Serialize(in server);
        var clientStore = new DataStore();
        int clientEntity = clientStore.Alloc();
        Assert.True(codec.Apply(clientStore, clientEntity, payload).Success);
        Assert.True(clientStore.TryGet(clientEntity, out InventoryComponent client));
        Assert.Empty(client.Orphans);
        Assert.Equal(server.Get(in Backpack0), client.Get(in Backpack0));
    }

    [Fact]
    public void HostileWireCapacityOverflowAndUndefinedModesAreRejected()
    {
        var forged = new InventoryComponent
        {
            Groups =
            [
                new SlotGroupDescriptor(SlotGroups.HOTBAR, SlotGroupLayout.MAX_TOTAL_CAPACITY),
                new SlotGroupDescriptor(SlotGroups.BACKPACK, 1),
            ],
            Slots = [],
        };

        var codec = new InventoryCodec();
        byte[] bytes = codec.Serialize(in forged);
        var store = new DataStore();
        int entity = store.Alloc();
        Result applied = codec.Apply(store, entity, bytes);
        Assert.False(applied.Success);

        Assert.Throws<ArgumentOutOfRangeException>(() => SlotGroupLayout.Create([new SlotGroupDescriptor(SlotGroups.CARGO, int.MaxValue)]));

        InventoryComponent inv = InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.HOTBAR, 1));
        inv.Set(in Hotbar0, new ItemData("wb:rock", int.MaxValue - 1, int.MaxValue));
        Assert.False(inv.Add(new ItemData("wb:rock", 10, int.MaxValue)));
        Assert.Equal(int.MaxValue, inv.Get(in Hotbar0).Count);

        var badTransfer = new TransferOp(0UL, Hotbar0, 0UL, Backpack0, (TransferMode)99, null);
        Assert.False(InventoryResolver.Apply(ref inv, in badTransfer));
    }

    [Fact]
    public void DataCompositionResolvesSlotGroupSchemaDatabaseWithShoalLoggerFactoryAndHeadlessContainer()
    {
        using var shoalContainer = new Container();
        shoalContainer.RegisterInstance<ILoggerFactory>(NullLoggerFactory.Instance);
        MethodInfo loggerFactoryMethod = typeof(LoggerFactoryExtensions).GetMethod("CreateLogger", [typeof(ILoggerFactory)])!;
        shoalContainer.Register(typeof(ILogger<>), made: Made.Of(req => loggerFactoryMethod.MakeGenericMethod(req.Parent.ImplementationType)));
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine(AppContext.BaseDirectory, "TestFiles", "Items", "assets")));
        shoalContainer.RegisterInstance(vfs);
        shoalContainer.Register<IFileParseService, VirtualFileParseService>(Reuse.Transient);
        shoalContainer.RegisterDataServices();

        ISlotGroupSchemaDatabase loaded = shoalContainer.Resolve<ISlotGroupSchemaDatabase>();
        Assert.True(loaded.Get(SlotGroups.HOTBAR).Success);
        Assert.True(loaded.Get(SlotGroups.BACKPACK).Success);
        Assert.True(loaded.Get(SlotGroups.WEARABLES).Success);
        Assert.True(loaded.Get(SlotGroups.AUGMENTS).Success);
        Assert.True(loaded.Get(SlotGroups.FUEL).Success);

        using var headlessContainer = new Container();
        headlessContainer.RegisterDataServices();

        ISlotGroupSchemaDatabase headless = headlessContainer.Resolve<ISlotGroupSchemaDatabase>();
        Assert.False(headless.Get(SlotGroups.HOTBAR).Success);
    }

    private sealed class StubItemDatabase(params Item[] items) : IItemDatabase
    {
        private readonly Dictionary<string, Item> _items = items.ToDictionary(item => item.ID, StringComparer.Ordinal);

        public Result<Item> Get(string id)
        {
            return _items.TryGetValue(id, out Item? item)
                ? Result<Item>.FromSuccess(item)
                : Result<Item>.FromFailure($"Unknown item \"{id}\".");
        }
    }
}
