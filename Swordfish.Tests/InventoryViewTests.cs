#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Reef;
using Reef.UI;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI.Layers;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Items;
using Xunit;

namespace Swordfish.Tests;

/// <summary>Pins the markup inventory's layout, footer, and slot gestures.</summary>
public class InventoryViewTests
{
    private static readonly int[] _widths = [800, 1280, 1920];
    private static readonly int[] _heights = [600, 720, 1080];

    [Fact]
    public void InventoryMatchesSnapshots()
    {
        for (var i = 0; i < _widths.Length; i++)
        {
            int width = _widths[i];
            int height = _heights[i];

            var empty = new Harness(width, height);
            AssertSnapshot($"{width}x{height}-inventory-empty", empty);

            var populated = new Harness(width, height);
            populated.Populate();
            AssertSnapshot($"{width}x{height}-inventory-populated", populated);

            var hovered = new Harness(width, height);
            hovered.Populate();
            hovered.Hover("inventorySlot22");
            AssertSnapshot($"{width}x{height}-inventory-hover", hovered);

            var dragging = new Harness(width, height);
            dragging.Populate();
            dragging.Hold("inventorySlot9");
            dragging.DragOver("inventorySlot20");
            AssertSnapshot($"{width}x{height}-inventory-drag", dragging);
        }
    }

    [Fact]
    public void HoveringASlotSelectsItAndLeavingClearsTheSelection()
    {
        var harness = new Harness();
        harness.Populate();

        harness.Hover("inventorySlot9");
        Assert.Equal(Harness.SlotHoverFill, harness.FindCommand("inventorySlot9").Color);

        harness.MoveAway();
        Assert.Equal(Harness.SlotFill, harness.FindCommand("inventorySlot9").Color);
    }

    [Fact]
    public void ToggleOpensWithAnExclusiveInteractionBlockerAndCloses()
    {
        var harness = new Harness();

        harness.InvokeShortcut("Inventory");
        Assert.True(harness.InteractionState.IsInteractionBlocked());

        harness.InvokeShortcut("Inventory");
        Assert.False(harness.InteractionState.IsInteractionBlocked());
    }

    [Fact]
    public void RightClickSplitsAStackIntoTheFirstEmptySlot()
    {
        var harness = new Harness();
        harness.Populate();

        harness.RightClick("inventorySlot9");

        Assert.Equal(3, harness.Slot(9).Count);
        Assert.Equal(2, harness.Slot(2).Count);
    }

    [Fact]
    public void ShiftClickAutoStacksIntoCargo()
    {
        var harness = new Harness();
        harness.Populate();

        harness.HoldKey(Key.Shift);
        harness.Click("inventorySlot0");
        harness.ReleaseKey(Key.Shift);

        Assert.True(IsEmpty(harness.Slot(0)));
        Assert.Equal(17, harness.Slot(9).Count);
    }

    [Fact]
    public void HoldingASlotStartsADragAndReleaseMovesIt()
    {
        var harness = new Harness();
        harness.Populate();

        harness.Hold("inventorySlot9");
        Assert.Equal(Harness.SlotDragFill, harness.FindCommand("inventorySlot9").Color);
        Assert.Equal(2, harness.CountBadgeText("5"));

        harness.ReleaseOver("inventorySlot10");

        Assert.True(IsEmpty(harness.Slot(9)));
        Assert.Equal(5, harness.Slot(10).Count);
    }

    [Fact]
    public void HoveringAnUnknownItemFallsBackToItsId()
    {
        var harness = new Harness();
        harness.Populate();

        harness.Hover("inventorySlot13");

        Assert.Equal(1, harness.CountText("wb:unknown"));
    }

    [Fact]
    public void ScrollingWhileDraggingMovesOneItemInEitherDirection()
    {
        var harness = new Harness();
        harness.Populate();

        harness.Hold("inventorySlot9");
        harness.ScrollOver("inventorySlot10", 1f);

        Assert.Equal(4, harness.Slot(9).Count);
        Assert.Equal(1, harness.Slot(10).Count);
    }

    private static bool IsEmpty(ItemData item)
    {
        return item.Count <= 0 || string.IsNullOrEmpty(item.ID);
    }

    private static void AssertSnapshot(string name, Harness harness)
    {
        string actual = UiSnapshotTests.Capture(harness.Step());
        string goldenPath = Path.Combine(SourceDirectory(), "TestFiles", "Snapshots", name + ".txt");
        bool update = string.Equals(
            Environment.GetEnvironmentVariable("UI_SNAPSHOT_UPDATE"),
            "1",
            StringComparison.Ordinal
        );
        if (!File.Exists(goldenPath) || update)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.WriteAllText(goldenPath, actual);
            return;
        }

        Assert.True(
            string.Equals(File.ReadAllText(goldenPath), actual, StringComparison.Ordinal),
            $"Snapshot mismatch for {name}."
        );
    }

    private static string SourceDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }

    private sealed class Harness
    {
        private const int SLOT_COUNT = 45;

        /// <summary>The idle slot fill, from the shared <c>wb-slot</c> class.</summary>
        public static readonly Vector4 SlotFill = FromRgb(21, 29, 42);

        /// <summary>The hovered slot fill, from the shared <c>wb-slot-hovered</c> class.</summary>
        public static readonly Vector4 SlotHoverFill = FromRgb(27, 37, 56);

        /// <summary>The dragged slot fill, from the shared <c>wb-slot-dragging</c> class.</summary>
        public static readonly Vector4 SlotDragFill = FromRgb(22, 40, 56);

        private readonly UIController _controller = new();
        private readonly UIBuilder<Material> _ui;
        private readonly RecordingShortcutService _shortcuts = new();
        private readonly FakeInputService _input = new();
        private readonly World _world = new();
        private readonly int _player;
        private readonly Inventory _inventory;
        private List<RenderCommand<Material>> _last = [];
        private double _delta;
        private int _cursorX;
        private int _cursorY;
        private UIController.MouseButtons _buttons;

        public Harness(int width = 1280, int height = 720)
        {
            _ui = new UIBuilder<Material>(width, height, new StubTextEngine(), _controller);

            var bricks = new FakeBrickDatabase(Bricks());
            var items = new FakeItemDatabase(Items());
            var icons = new ItemIconDatabase(
                NullLogger<ItemIconDatabase>.Instance,
                new StubFileParseService(),
                new StubTextureDatabase(),
                items
            );

            var motionProcessor = new ClientPlayerMotionProcessor(
                _input,
                new FakeWindowContext(),
                new FakePhysics(),
                new EventInvoker<PlayerMovedEvent>([])
            );

            InteractionState = new InteractionState(motionProcessor);
            PlayerData = new PlayerData(items);

            _player = _world.DataStore.Alloc();
            _world.DataStore.AddOrUpdate(_player, new PlayerComponent());
            _world.DataStore.AddOrUpdate(_player, new InventoryComponent(SLOT_COUNT));
            _world.DataStore.AddOrUpdate(_player, new EquipmentComponent());
            _world.DataStore.AddOrUpdate(_player, new PendingInventoryComponent());

            _inventory = new Inventory(
                _shortcuts,
                items,
                bricks,
                icons,
                PlayerData,
                new TestEcsContext(_world),
                InteractionState,
                _input,
                new InventoryLocalization()
            );
        }

        public InteractionState InteractionState { get; }

        public PlayerData PlayerData { get; }

        /// <summary>Advances the pointer state and renders one frame, like the renderer does.</summary>
        public List<RenderCommand<Material>> Step(UIController.MouseButtons? buttons = null)
        {
            if (buttons.HasValue)
            {
                _buttons = buttons.Value;
            }

            _controller.UpdateMouse(_cursorX, _cursorY, _buttons);
            _input.CursorPosition = new Vector2(_cursorX, _cursorY);
            _delta += 0.05d;
            _inventory.RenderUI(_delta, _ui);
            _last = _ui.Build(_delta);
            return _last;
        }

        public void Populate()
        {
            SetSlot(0, new ItemData("wb:rock", 12, 64));
            SetSlot(1, new ItemData("wb:torch", 3, 32));
            SetSlot(9, new ItemData("wb:rock", 5, 64));
            SetSlot(13, new ItemData("wb:unknown", 1, 1));
            SetSlot(20, new ItemData("wb:control_buttons", 1, 16));
            SetSlot(22, new ItemData("wb:caution", 1, 16));
            SetSlot(41, new ItemData("wb:rock", 64, 64));
            Step();
        }

        public ItemData Slot(int slot)
        {
            Assert.True(_world.DataStore.TryGet(_player, out InventoryComponent inventory));
            return inventory.Contents![slot];
        }

        public RenderCommand<Material> FindCommand(string id)
        {
            for (var i = 0; i < _last.Count; i++)
            {
                if (_last[i].ID == id)
                {
                    return _last[i];
                }
            }

            throw new Xunit.Sdk.XunitException($"No render command found for \"{id}\".");
        }

        /// <summary>Counts the visible text commands that carry exactly this text.</summary>
        public int CountText(string text)
        {
            var count = 0;
            for (var i = 0; i < _last.Count; i++)
            {
                if (string.Equals(_last[i].Text, text, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Counts the scrim-backed text commands that carry exactly this text.</summary>
        public int CountBadgeText(string text)
        {
            var count = 0;
            for (var i = 0; i < _last.Count; i++)
            {
                if (string.Equals(_last[i].Text, text, StringComparison.Ordinal)
                    && _last[i].BackgroundColor.W > 0f)
                {
                    count++;
                }
            }

            return count;
        }

        public void InvokeShortcut(string name)
        {
            for (var i = 0; i < _shortcuts.Shortcuts.Count; i++)
            {
                Shortcut shortcut = _shortcuts.Shortcuts[i];
                if (string.Equals(shortcut.Name, name, StringComparison.Ordinal))
                {
                    shortcut.Action();
                    return;
                }
            }

            throw new Xunit.Sdk.XunitException($"No shortcut named \"{name}\".");
        }

        public void Hover(string id)
        {
            Step();
            MoveTo(id);
            Step();
            Step();
        }

        public void MoveAway()
        {
            _cursorX = 1;
            _cursorY = 1;
            Step();
            Step();
            Step();
        }

        public void Click(string id)
        {
            Hover(id);
            Step(UIController.MouseButtons.Left);
            Release();
        }

        public void RightClick(string id)
        {
            Hover(id);
            Step(UIController.MouseButtons.Right);
            Release();
        }

        public void Hold(string id)
        {
            Hover(id);
            Step(UIController.MouseButtons.Left);
            Step();
            Step();
        }

        public void ReleaseOver(string id)
        {
            MoveTo(id);
            Step(UIController.MouseButtons.Left);
            Step();
            Release();
        }

        public void DragOver(string id)
        {
            MoveTo(id);
            Step();
            Step();
        }

        public void ScrollOver(string id, float delta)
        {
            MoveTo(id);
            Step(UIController.MouseButtons.Left);
            Step();

            _input.Scroll = delta;
            Step();
            _input.Scroll = 0f;
        }

        public void HoldKey(Key key) => _input.HeldKeys.Add(key);

        public void ReleaseKey(Key key) => _input.HeldKeys.Remove(key);

        private void MoveTo(string id)
        {
            ElementBounds bounds = _ui.GetElementBounds(id);
            _cursorX = bounds.Rect.Left + bounds.Rect.Size.X / 2;
            _cursorY = bounds.Rect.Top + bounds.Rect.Size.Y / 2;
        }

        private void Release()
        {
            Step(UIController.MouseButtons.None);
            Step();
        }

        private void SetSlot(int slot, ItemData item)
        {
            PlayerData.MutateInventory(_world.DataStore, (ref InventoryComponent inventory) =>
            {
                inventory.Contents![slot] = item;
            });
        }

        private static Vector4 FromRgb(byte r, byte g, byte b)
        {
            return new Vector4(r / 255f, g / 255f, b / 255f, 1f);
        }

        private static Brick[] Bricks()
        {
            return
            [
                new Brick("wb:rock", BrickAlpha.Opaque, false, null, BrickShape.Any, default, null),
                new Brick("wb:caution", BrickAlpha.Opaque, false, null, BrickShape.Any, default, null),
                new Brick("wb:control_buttons", BrickAlpha.Opaque, false, null, BrickShape.Block, default, ["orientable"]),
            ];
        }

        private static Item[] Items()
        {
            return
            [
                new Item(
                    "wb:rock",
                    "Rock",
                    "rock.png",
                    64,
                    new PlaceableDefinition { Type = PlaceableType.Brick, ID = "wb:rock" },
                    null,
                    null,
                    null
                ),
                new Item("wb:torch", "Torch", "torch.png", 32, null, null, null, null),
                new Item(
                    "wb:control_buttons",
                    "Control buttons",
                    "buttons.png",
                    16,
                    new PlaceableDefinition { Type = PlaceableType.Brick, ID = "wb:control_buttons" },
                    null,
                    null,
                    null
                ),
                new Item(
                    "wb:caution",
                    "Caution panel",
                    "caution.png",
                    16,
                    new PlaceableDefinition { Type = PlaceableType.Brick, ID = "wb:caution" },
                    null,
                    null,
                    null
                ),
            ];
        }
    }

    private sealed class RecordingShortcutService : IShortcutService
    {
        public List<Shortcut> Shortcuts { get; } = [];

        public bool RegisterShortcut(Shortcut shortcut)
        {
            Shortcuts.Add(shortcut);
            return true;
        }
    }

    private sealed class TestEcsContext(World world) : IECSContext
    {
        public DataBinding<double> Delta { get; } = new();

        public World World { get; } = world;
    }

    private sealed class InventoryLocalization : ILocalization
    {
        public string? GetString(string value)
        {
            return value switch
            {
                "ui.label.inventory.title" => "Inventory",
                "ui.label.inventory.empty" => "Empty slot",
                "ui.label.inventory.shapeable" => "Shapeable",
                "ui.label.inventory.orientable" => "Rotatable",
                _ => null,
            };
        }

        public string? GetString(string value, string cultureName) => GetString(value);

        public string? GetString(string value, CultureInfo cultureInfo) => GetString(value);
    }

    private sealed class FakeItemDatabase : IAssetDatabase<Item>
    {
        private readonly Dictionary<string, Item> _items;

        public FakeItemDatabase(Item[] items)
        {
            _items = items.ToDictionary(item => item.ID, StringComparer.Ordinal);
        }

        public Result<Item> Get(string id)
        {
            return _items.TryGetValue(id, out Item? item)
                ? Result<Item>.FromSuccess(item)
                : Result<Item>.FromFailure($"Unknown item \"{id}\".");
        }
    }

    private sealed class FakeBrickDatabase : IBrickDatabase
    {
        private readonly Dictionary<string, Brick> _bricks;

        public FakeBrickDatabase(Brick[] bricks)
        {
            _bricks = bricks.ToDictionary(brick => brick.ID, StringComparer.Ordinal);
        }

        public Result<Brick> Get(string id)
        {
            return _bricks.TryGetValue(id, out Brick? brick)
                ? Result<Brick>.FromSuccess(brick)
                : Result<Brick>.FromFailure($"Unknown brick \"{id}\".");
        }

        public Result<Brick> Get(ushort id) => Result<Brick>.FromFailure("Unknown brick.");

        public List<Brick> Get(Func<Brick, bool> predicate) => [.. _bricks.Values.Where(predicate)];

        public bool IsCuller(in Voxel voxel, BrickShape shape) => false;
    }
}
