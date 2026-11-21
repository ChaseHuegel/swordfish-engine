#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Krill;
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
using Swordfish.Tests;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI.Layers;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Items;
using Xunit;

namespace WaywardBeyond.Tests;

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
    public void DashedDividerCarriesTheSharedRulePattern()
    {
        var harness = new Harness();
        harness.Step();

        RenderCommand<Material> rule = harness.FindCommand("Inventory.Divider");

        Assert.Equal(1, rule.Rect.Size.Y);
        Assert.Equal(1, rule.Border.BottomThickness);
        Assert.Equal(2, rule.Border.Dash);
        Assert.Equal(4, rule.Border.Gap);
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
        Assert.Equal(1, harness.CountText("Unknown item"));
    }

    [Fact]
    public void TooltipIsHiddenWhenIdleOrHoveringEmptySlot()
    {
        var harness = new Harness();
        harness.Populate();

        Assert.False(harness.HasCommand("Inventory.Tooltip"));

        harness.Hover("inventorySlot10");
        Assert.False(harness.HasCommand("Inventory.Tooltip"));
    }

    [Fact]
    public void TooltipAnchorsTopRightOnFirstHoverFrameAndHidesWhileDragging()
    {
        var harness = new Harness(1280, 720);
        harness.Populate();

        harness.HoverOnce("inventorySlot9");

        RenderCommand<Material> slot = harness.FindCommand("inventorySlot9");
        RenderCommand<Material> tooltip = harness.FindCommand("Inventory.Tooltip");
        Assert.Equal(slot.Rect.Right + 8, tooltip.Rect.Left);
        Assert.Equal(slot.Rect.Top, tooltip.Rect.Top);

        harness.Hold("inventorySlot9");
        Assert.False(harness.HasCommand("Inventory.Tooltip"));
    }

    [Fact]
    public void TooltipFlipsLeftNearRightViewportEdge()
    {
        var harness = new Harness(600, 600);
        harness.Populate();

        harness.HoverOnce("inventorySlot17");

        RenderCommand<Material> slot = harness.FindCommand("inventorySlot17");
        RenderCommand<Material> tooltip = harness.FindCommand("Inventory.Tooltip");
        Assert.Equal(slot.Rect.Left - 8, tooltip.Rect.Right);
        Assert.Equal(slot.Rect.Top, tooltip.Rect.Top);
        Assert.True(tooltip.Rect.Left >= 8);
    }

    [Fact]
    public void TooltipFlipsUpNearBottomViewportEdge()
    {
        var harness = new Harness(1280, 400);
        harness.Populate();

        harness.HoverOnce("inventorySlot0");

        RenderCommand<Material> slot = harness.FindCommand("inventorySlot0");
        RenderCommand<Material> tooltip = harness.FindCommand("Inventory.Tooltip");
        Assert.Equal(slot.Rect.Right + 8, tooltip.Rect.Left);
        Assert.Equal(slot.Rect.Bottom, tooltip.Rect.Bottom);
        Assert.True(tooltip.Rect.Bottom <= 400 - 8);
    }

    [Fact]
    public void TooltipShowsSubtitleOptionalDescriptionAndTraits()
    {
        var harness = new Harness(1280, 720);
        harness.Populate();

        // Brick with description and two traits
        harness.Hover("inventorySlot22");
        Assert.Equal(1, harness.CountText("Caution panel"));
        Assert.Equal(1, harness.CountText("Brick"));
        Assert.Equal(1, harness.CountText("Painted steel plating"));
        Assert.Equal(1, harness.CountText("Rotatable"));
        Assert.Equal(1, harness.CountText("Shapeable"));

        // Brick with three traits wraps onto two rows inside max-width
        harness.Hover("inventorySlot12");
        RenderCommand<Material> lightTooltip = harness.FindCommand("Inventory.Tooltip");
        Assert.Equal(1, harness.CountText("Light"));
        Assert.Equal(1, harness.CountText("Brick"));
        Assert.Equal(1, harness.CountText("Rotatable"));
        Assert.Equal(1, harness.CountText("Shapeable"));
        Assert.Equal(1, harness.CountText("Light source"));
        Assert.True(lightTooltip.Rect.Size.X <= 268);

        // Thruster shows propulsion trait
        harness.Hover("inventorySlot17");
        Assert.Equal(1, harness.CountText("Thruster"));
        Assert.Equal(1, harness.CountText("Rotatable"));
        Assert.Equal(1, harness.CountText("Propulsion"));

        // Tool shows mining tool subtitle and no brick traits
        harness.Hover("inventorySlot3");
        Assert.Equal(1, harness.CountText("Mining Laser"));
        Assert.Equal(1, harness.CountText("Mining tool"));
        Assert.Equal(1, harness.CountText("Handheld beam cutter"));
        Assert.Equal(0, harness.CountText("Rotatable"));

        // Brick without description omits description cleanly
        harness.Hover("inventorySlot9");
        Assert.Equal(1, harness.CountText("Rock"));
        Assert.Equal(1, harness.CountText("Brick"));
        Assert.Equal(0, harness.CountText("Painted steel plating"));
    }

    [Fact]
    public void TooltipShowsPaintAndTextureToolSubtitlesForSprayerAndDrill()
    {
        var harness = new Harness(1280, 720);
        harness.Populate();
        harness.PutItem(4, new ItemData("wb:sprayer", 1, 1));
        harness.PutItem(5, new ItemData("wb:drill", 1, 1));

        harness.Hover("inventorySlot4");
        Assert.Equal(1, harness.CountText("Paint Sprayer"));
        Assert.Equal(1, harness.CountText("Paint tool"));

        harness.Hover("inventorySlot5");
        Assert.Equal(1, harness.CountText("Drill"));
        Assert.Equal(1, harness.CountText("Texture tool"));
    }

    [Fact]
    public void TooltipShowsItemIdSecondSubtitleRowWhenDebugOverlayEnabled()
    {
        var harness = new Harness(1280, 720);
        harness.Populate();

        harness.Hover("inventorySlot22");
        Assert.Equal(0, harness.CountText("wb:caution"));

        harness.DebugSettings.OverlayVisible.Set(true);
        harness.Hover("inventorySlot22");
        Assert.Equal(1, harness.CountText("wb:caution"));

        RenderCommand<Material> kindSubtitle = harness.FindText("Brick");
        RenderCommand<Material> idSubtitle = harness.FindText("wb:caution");
        RenderCommand<Material> description = harness.FindText("Painted steel plating");
        Assert.True(idSubtitle.Rect.Top >= kindSubtitle.Rect.Bottom);
        Assert.True(description.Rect.Top >= idSubtitle.Rect.Bottom);

        harness.DebugSettings.OverlayVisible.Set(false);
        harness.Hover("inventorySlot22");
        Assert.Equal(0, harness.CountText("wb:caution"));
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

            DebugSettings = new DebugSettings();
            _inventory = new Inventory(
                _shortcuts,
                items,
                bricks,
                icons,
                PlayerData,
                new TestEcsContext(_world),
                InteractionState,
                _input,
                new InventoryLocalization(),
                DebugSettings
            );
        }

        public InteractionState InteractionState { get; }

        public PlayerData PlayerData { get; }

        public DebugSettings DebugSettings { get; }

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
            SetSlot(3, new ItemData("laser", 1, 1));
            SetSlot(9, new ItemData("wb:rock", 5, 64));
            SetSlot(12, new ItemData("wb:light", 4, 64));
            SetSlot(13, new ItemData("wb:unknown", 1, 1));
            SetSlot(17, new ItemData("wb:thruster", 2, 16));
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

        public bool HasCommand(string id)
        {
            for (var i = 0; i < _last.Count; i++)
            {
                if (_last[i].ID == id)
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>Finds the first render command whose text matches exactly.</summary>
        public RenderCommand<Material> FindText(string text)
        {
            for (var i = 0; i < _last.Count; i++)
            {
                if (string.Equals(_last[i].Text, text, StringComparison.Ordinal))
                {
                    return _last[i];
                }
            }

            throw new Xunit.Sdk.XunitException($"No render command found with text \"{text}\".");
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

        public void HoverOnce(string id)
        {
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

        public void PutItem(int slot, ItemData item) => SetSlot(slot, item);

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
                new Brick("wb:light", BrickAlpha.Blend, false, null, BrickShape.Any, default, ["light"]),
                new Brick("wb:thruster", BrickAlpha.Opaque, false, null, BrickShape.Custom, default, ["orientable", "thruster"]),
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
                    "laser",
                    "Mining Laser",
                    "laser.png",
                    1,
                    null,
                    new ToolDefinition { Type = ToolType.Mining },
                    null,
                    null,
                    "item.base.laser.description"
                ),
                new Item(
                    "wb:sprayer",
                    "Paint Sprayer",
                    "sprayer.png",
                    1,
                    null,
                    new ToolDefinition { Type = ToolType.Sprayer },
                    null,
                    null
                ),
                new Item(
                    "wb:drill",
                    "Drill",
                    "drill.png",
                    1,
                    null,
                    new ToolDefinition { Type = ToolType.Drill },
                    null,
                    null
                ),
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
                    null,
                    "item.base.caution.description"
                ),
                new Item(
                    "wb:light",
                    "Light",
                    "light.png",
                    64,
                    new PlaceableDefinition { Type = PlaceableType.Brick, ID = "wb:light" },
                    null,
                    null,
                    null,
                    "item.base.light.description"
                ),
                new Item(
                    "wb:thruster",
                    "Thruster",
                    "thruster.png",
                    16,
                    new PlaceableDefinition { Type = PlaceableType.Brick, ID = "wb:thruster" },
                    null,
                    null,
                    null,
                    "item.base.thruster.description"
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
                "ui.label.item.kind.brick" => "Brick",
                "ui.label.item.kind.miningTool" => "Mining tool",
                "ui.label.item.kind.conversionTool" => "Conversion tool",
                "ui.label.item.kind.paintTool" => "Paint tool",
                "ui.label.item.kind.textureTool" => "Texture tool",
                "ui.label.item.kind.item" => "Item",
                "ui.label.item.kind.unknown" => "Unknown item",
                "ui.label.item.trait.orientable" => "Rotatable",
                "ui.label.item.trait.shapeable" => "Shapeable",
                "ui.label.item.trait.lightSource" => "Light source",
                "ui.label.item.trait.propulsion" => "Propulsion",
                "item.base.caution.description" => "Painted steel plating",
                "item.base.light.description" => "Caged deck lamp that casts bright light across corridors",
                "item.base.laser.description" => "Handheld beam cutter",
                "item.base.thruster.description" => "Exhaust nozzle assembly",
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
