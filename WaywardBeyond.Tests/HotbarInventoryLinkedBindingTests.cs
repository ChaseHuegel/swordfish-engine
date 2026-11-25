#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Reef;
using Reef.UI;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Input;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Tests;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI.Layers;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Items;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Verifies that Hotbar and Inventory share one 1-9 slot shortcut set and live corner labels.</summary>
[Collection("ClientGameState")]
public class HotbarInventoryLinkedBindingTests
{
    [Fact]
    public void RebindingSharedSlotShortcutUpdatesHotbarInventoryAndLabels()
    {
        WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.Playing);
        try
        {
            var input = new FakeInputService();
            var shortcuts = new ShortcutService(input, NullLogger.Instance);
            var world = new World();
            int player = world.DataStore.Alloc();
            world.DataStore.AddOrUpdate(player, new PlayerComponent());
            world.DataStore.AddOrUpdate(player, new InventoryComponent(45));
            world.DataStore.AddOrUpdate(player, new EquipmentComponent { ActiveInventorySlot = 3 });
            world.DataStore.AddOrUpdate(player, new PendingInventoryComponent());

            var items = new FakeItemDatabase([new Item("wb:brick", "Brick", "brick.png", 64, null, null, null, null)]);
            var bricks = new FakeBrickDatabase([]);
            var icons = new ItemIconDatabase(NullLogger<ItemIconDatabase>.Instance, new StubFileParseService(), new StubTextureDatabase(), items);
            var playerData = new PlayerData(items);
            playerData.MutateInventory(world.DataStore, (ref InventoryComponent inventory) =>
            {
                inventory.Contents![9] = new ItemData("wb:brick", 5, 64);
            });
            var ecs = new TestEcsContext(world);
            var motionProcessor = new ClientPlayerMotionProcessor(input, new FakeWindowContext(), new FakePhysics(), new EventInvoker<PlayerMovedEvent>([]));
            var interactionState = new InteractionState(motionProcessor);
            var controlSettings = new ControlSettings();

            var hotbar = new Hotbar(shortcuts, input, icons, playerData, ecs, interactionState, controlSettings);
            var inventory = new Inventory(
                shortcuts,
                items,
                bricks,
                new StubLocalizedTagsDatabase(),
                icons,
                playerData,
                ecs,
                interactionState,
                input,
                new TestLocalization(),
                new DebugSettings()
            );

            //  Only one shared Inventory/Slot 1..9 group exists in ShortcutService.
            IReadOnlyList<ShortcutInfo> registered = shortcuts.GetShortcuts();
            Assert.Equal(1, registered.Count(info => info.Id == GameShortcuts.Slot(0)));
            Assert.Equal(Hotbar.SLOT_COUNT, registered.Count(info => info.Id.StartsWith("Inventory/Slot ", StringComparison.Ordinal)));

            //  Rebind Slot 1 to Numpad1.
            Assert.True(shortcuts.Rebind(GameShortcuts.Slot(0), new ShortcutBinding(Key.Numpad1)).Success);

            //  Old key D1 no longer selects slot 0; Numpad1 selects slot 0 when inventory is closed.
            input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.D1));
            Assert.Equal(3, playerData.GetActiveSlot(world.DataStore).Value);

            input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Numpad1));
            Assert.Equal(0, playerData.GetActiveSlot(world.DataStore).Value);

            var hotbarUi = new UIBuilder<Material>(1280, 720, new StubTextEngine(), new UIController());
            hotbar.RenderUI(0d, hotbarUi);
            List<RenderCommand<Material>> hotbarCommands = hotbarUi.Build(0d);
            Assert.Contains(hotbarCommands, command => command.Text == "Num 1");

            //  Open inventory, hover cargo slot 9, and press Numpad1 to move stack into slot 0.
            input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Tab));
            var controller = new UIController();
            var invUi = new UIBuilder<Material>(1280, 720, new StubTextEngine(), controller);
            inventory.RenderUI(0d, invUi);
            invUi.Build(0d);

            ElementBounds slot9Bounds = invUi.GetElementBounds("inventorySlot9");
            controller.UpdateMouse(slot9Bounds.Rect.Left + slot9Bounds.Rect.Size.X / 2, slot9Bounds.Rect.Top + slot9Bounds.Rect.Size.Y / 2, UIController.MouseButtons.None);
            inventory.RenderUI(0.016d, invUi);
            invUi.Build(0.016d);
            inventory.RenderUI(0.032d, invUi);
            List<RenderCommand<Material>> invCommands = invUi.Build(0.032d);

            Assert.Contains(invCommands, command => command.Text == "Num 1");

            input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Numpad1));
            InventoryComponent updated = playerData.GetInventory(world.DataStore).Value;
            Assert.Equal("wb:brick", updated.Contents[0].ID);
            Assert.Equal(5, updated.Contents[0].Count);
            Assert.Equal(0, updated.Contents[9].Count);
        }
        finally
        {
            WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.MainMenu);
        }
    }

    private sealed class StubLocalizedTagsDatabase : ILocalizedTagsDatabase
    {
        public LocalizedTags? Invariant { get; } = new(new Dictionary<string, List<string>>());

        public Result<LocalizedTags> Get(string id) => Result<LocalizedTags>.FromSuccess(Invariant!);
    }

    private sealed class TestEcsContext(World world) : IECSContext
    {
        public DataBinding<double> Delta { get; } = new();

        public World World { get; } = world;
    }

    private sealed class FakeItemDatabase(Item[] items) : IAssetDatabase<Item>
    {
        private readonly Dictionary<string, Item> _items = items.ToDictionary(item => item.ID, StringComparer.Ordinal);

        public Result<Item> Get(string id) =>
            _items.TryGetValue(id, out Item? item)
                ? Result<Item>.FromSuccess(item)
                : Result<Item>.FromFailure($"Unknown item \"{id}\".");
    }

    private sealed class FakeBrickDatabase(Brick[] bricks) : IBrickDatabase
    {
        private readonly Dictionary<string, Brick> _bricks = bricks.ToDictionary(brick => brick.ID, StringComparer.Ordinal);

        public Result<Brick> Get(string id) =>
            _bricks.TryGetValue(id, out Brick? brick)
                ? Result<Brick>.FromSuccess(brick)
                : Result<Brick>.FromFailure($"Unknown brick \"{id}\".");

        public Result<Brick> Get(ushort id) => Result<Brick>.FromFailure("Unknown brick.");

        public List<Brick> Get(Func<Brick, bool> predicate) => [.. _bricks.Values.Where(predicate)];

        public bool IsCuller(in Voxel voxel, BrickShape shape) => false;
    }
}
