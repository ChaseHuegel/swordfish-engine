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
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.Skills;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI.Layers;
using WaywardBeyond.Client.UI.Layers.Skills;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Items;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Covers dynamic live keycaps and preserved hint sets in <see cref="ControlHints"/>.</summary>
[Collection("ClientGameState")]
public class ControlHintsTests
{
    [Fact]
    public void ControlHintsKeepAllExistingHintsAndUpdateKeycapsOnRebind()
    {
        WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.Playing);
        try
        {
            var harness = new Harness();
            harness.EquipItem("wb:brick");

            List<string> placeableTexts = harness.RenderTexts();
            Assert.Contains("R", placeableTexts);
            Assert.Contains("Change shape", placeableTexts);
            Assert.Contains("T", placeableTexts);
            Assert.Contains("Change orientation", placeableTexts);
            Assert.Contains("Shift", placeableTexts);
            Assert.Contains("Snap brick", placeableTexts);
            Assert.Contains("MMB", placeableTexts);
            Assert.Contains("Pick brick", placeableTexts);
            Assert.Contains("Tab", placeableTexts);
            Assert.Contains("Inventory", placeableTexts);
            Assert.Contains("F8", placeableTexts);
            Assert.Contains("Send feedback", placeableTexts);

            harness.Shortcuts.Rebind(GameShortcuts.CHANGE_SHAPE, new ShortcutBinding(Key.G));
            harness.Shortcuts.Rebind(GameShortcuts.CHANGE_ORIENTATION, new ShortcutBinding(Key.Y));
            harness.Shortcuts.Rebind(GameShortcuts.SNAP_PLACEMENT, new ShortcutBinding(Key.Control));
            harness.Shortcuts.Rebind(GameShortcuts.PICK_BRICK, new ShortcutBinding(MouseButton.Right));
            harness.Shortcuts.Rebind(GameShortcuts.INVENTORY, new ShortcutBinding(Key.I));
            harness.Shortcuts.Rebind(GameShortcuts.FEEDBACK_FORM, new ShortcutBinding(Key.F9));

            List<string> reboundPlaceableTexts = harness.RenderTexts();
            Assert.Contains("G", reboundPlaceableTexts);
            Assert.Contains("Y", reboundPlaceableTexts);
            Assert.Contains("Ctrl", reboundPlaceableTexts);
            Assert.Contains("RMB", reboundPlaceableTexts);
            Assert.Contains("I", reboundPlaceableTexts);
            Assert.Contains("F9", reboundPlaceableTexts);
            Assert.DoesNotContain("R", reboundPlaceableTexts);

            harness.EquipItem("wb:sprayer");
            harness.Shortcuts.Rebind(GameShortcuts.SELECT_PAINT_COLOR, new ShortcutBinding(Key.C));
            List<string> sprayerTexts = harness.RenderTexts();
            Assert.Contains("C", sprayerTexts);
            Assert.Contains("Change color", sprayerTexts);

            harness.EquipItem("wb:drill");
            harness.Shortcuts.Rebind(GameShortcuts.SELECT_PAINT_TEXTURE, new ShortcutBinding(Key.V));
            List<string> drillTexts = harness.RenderTexts();
            Assert.Contains("V", drillTexts);
            Assert.Contains("Change texture", drillTexts);

            harness.OpenInventory();
            List<string> inventoryTexts = harness.RenderTexts();
            Assert.Contains("LMB", inventoryTexts);
            Assert.Contains("Move / stack", inventoryTexts);
            Assert.Contains("RMB", inventoryTexts);
            Assert.Contains("Split half", inventoryTexts);
            Assert.Contains("Shift+LMB", inventoryTexts);
            Assert.Contains("Quick move", inventoryTexts);
            Assert.Contains("1-9", inventoryTexts);
            Assert.Contains("Move to hotbar", inventoryTexts);
            Assert.Contains("I / Esc", inventoryTexts);
            Assert.Contains("Close", inventoryTexts);

            for (var i = 0; i < Hotbar.SLOT_COUNT; i++)
            {
                harness.Shortcuts.Rebind(GameShortcuts.Slot(i), new ShortcutBinding(Key.Numpad1 + i));
            }

            List<string> numpadInventoryTexts = harness.RenderTexts();
            Assert.Contains("Num 1-9", numpadInventoryTexts);

            harness.Shortcuts.Rebind(GameShortcuts.Slot(0), new ShortcutBinding(Key.F1));
            harness.Shortcuts.Rebind(GameShortcuts.Slot(8), new ShortcutBinding(Key.F9));
            List<string> customRangeTexts = harness.RenderTexts();
            Assert.Contains("F1-F9", customRangeTexts);

            harness.Shortcuts.Rebind(GameShortcuts.INVENTORY, ShortcutBinding.Unbound);
            harness.Shortcuts.Rebind(GameShortcuts.INVENTORY_MOVE_STACK, new ShortcutBinding(MouseButton.Right));
            harness.Shortcuts.Rebind(GameShortcuts.INVENTORY_SPLIT_STACK, new ShortcutBinding(Key.X));
            harness.Shortcuts.Rebind(GameShortcuts.INVENTORY_QUICK_MOVE, new ShortcutBinding(Key.Q));
            List<string> reboundInventoryActionTexts = harness.RenderTexts();
            Assert.Contains("Esc", reboundInventoryActionTexts);
            Assert.Contains("RMB", reboundInventoryActionTexts);
            Assert.Contains("Move / stack", reboundInventoryActionTexts);
            Assert.Contains("X", reboundInventoryActionTexts);
            Assert.Contains("Split half", reboundInventoryActionTexts);
            Assert.Contains("Q", reboundInventoryActionTexts);
            Assert.Contains("Quick move", reboundInventoryActionTexts);

            harness.Shortcuts.Rebind(GameShortcuts.INVENTORY, new ShortcutBinding(Key.Tab));
            harness.Shortcuts.Rebind(GameShortcuts.BACK, new ShortcutBinding(Key.Backspace));
            List<string> reboundBackTexts = harness.RenderTexts();
            Assert.Contains("Tab / Backspace", reboundBackTexts);
            Assert.Contains("Close", reboundBackTexts);

            harness.Shortcuts.Rebind(GameShortcuts.BACK, ShortcutBinding.Unbound);
            List<string> unboundBackTexts = harness.RenderTexts();
            Assert.Contains("Tab", unboundBackTexts);
            Assert.DoesNotContain("Backspace", unboundBackTexts);
            Assert.DoesNotContain("Esc", unboundBackTexts);

            harness.Shortcuts.Rebind(GameShortcuts.INVENTORY, ShortcutBinding.Unbound);
            List<string> bothUnboundTexts = harness.RenderTexts();
            Assert.Contains("Close", bothUnboundTexts);
            Assert.DoesNotContain("Tab", bothUnboundTexts);
            Assert.DoesNotContain("Esc", bothUnboundTexts);
        }
        finally
        {
            WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.MainMenu);
        }
    }

    [Fact]
    public void ControlHintsUpdatesWhenPersistedOverridesApplyOnLateRegistration()
    {
        WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.Playing);
        try
        {
            var harness = new Harness(registerLateShortcuts: false);
            harness.Shortcuts.ApplyOverrides(new Dictionary<string, ShortcutBinding>
            {
                [GameShortcuts.FEEDBACK_FORM] = new(Key.F9),
                [GameShortcuts.PICK_BRICK] = new(Key.G),
                [GameShortcuts.SNAP_PLACEMENT] = new(Key.Control),
            });

            //  Register the shortcuts after ControlHints has already constructed and initialized hints.
            harness.RegisterLateShortcuts();
            harness.EquipItem("wb:brick");

            List<string> texts = harness.RenderTexts();
            Assert.Contains("F9", texts);
            Assert.Contains("G", texts);
            Assert.Contains("Ctrl", texts);
            Assert.DoesNotContain("F8", texts);
        }
        finally
        {
            WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.MainMenu);
        }
    }

    [Fact]
    public void ControlHintsShowsSkillsHintAndHidesInventoryHintWhenSkillModalIsOpen()
    {
        WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.Playing);
        try
        {
            var harness = new Harness();

            List<string> idleTexts = harness.RenderTexts();
            Assert.Contains("Tab", idleTexts);
            Assert.Contains("Inventory", idleTexts);
            Assert.Contains("K", idleTexts);
            Assert.Contains("Skills", idleTexts);

            harness.OpenSkills();
            List<string> modalTexts = harness.RenderTexts();
            Assert.Contains("K / Esc", modalTexts);
            Assert.Contains("Close", modalTexts);
            Assert.DoesNotContain("Tab", modalTexts);
            Assert.DoesNotContain("Inventory", modalTexts);

            harness.Shortcuts.Rebind(GameShortcuts.SKILLS, new ShortcutBinding(Key.L));
            List<string> reboundModalTexts = harness.RenderTexts();
            Assert.Contains("L / Esc", reboundModalTexts);
            Assert.DoesNotContain("K / Esc", reboundModalTexts);
        }
        finally
        {
            WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.MainMenu);
        }
    }

    [Fact]
    public void ControlHintsShowsBlueprintPlaceableHintsAndHidesBrickAndProjectorHints()
    {
        WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.Playing);
        try
        {
            var harness = new Harness();
            harness.EquipItem("wb:core");

            List<string> texts = harness.RenderTexts();
            Assert.Contains("LMB", texts);
            Assert.Contains("Place", texts);
            Assert.Contains("T", texts);
            Assert.Contains("Rotate", texts);
            Assert.Contains("F", texts);
            Assert.Contains("Mirror", texts);
            Assert.Contains("Shift", texts);
            Assert.Contains("Embed", texts);
            Assert.Contains("Shift+Scroll", texts);
            Assert.Contains("Distance", texts);
            Assert.Contains("Inventory", texts);
            Assert.Contains("Skills", texts);

            Assert.DoesNotContain("Change shape", texts);
            Assert.DoesNotContain("Change orientation", texts);
            Assert.DoesNotContain("Pick brick", texts);
            Assert.DoesNotContain("Overwrite air", texts);
            Assert.DoesNotContain("Library", texts);
        }
        finally
        {
            WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.MainMenu);
        }
    }

    private sealed class Harness
    {
        private readonly FakeInputService _input = new();
        private readonly World _world = new();
        private readonly PlayerData _playerData;
        private readonly ControlHints _controlHints;
        private readonly Inventory _inventory;
        private readonly SkillsLayer _skillsLayer;
        private readonly UIBuilder<Material> _ui;

        public ShortcutService Shortcuts { get; }

        public Harness(bool registerLateShortcuts = true)
        {
            Shortcuts = new ShortcutService(_input, NullLogger.Instance);
            if (registerLateShortcuts)
            {
                RegisterLateShortcuts();
            }

            var bricks = new FakeBrickDatabase(
            [
                new Brick("wb:brick", BrickAlpha.Opaque, false, null, BrickShape.Any, default, orientable: true),
            ]);
            var items = new FakeItemDatabase(
            [
                new Item("wb:brick", "Brick", "brick.png", 64, new PlaceableDefinition { Type = PlaceableType.Brick, ID = "wb:brick" }, null, null, null),
                new Item("wb:core", "Core", "", 1, new PlaceableDefinition { Type = PlaceableType.Blueprint, ID = "builtin/core" }, null, null, null),
                new Item("wb:sprayer", "Sprayer", "sprayer.png", 1, null, new ToolDefinition { Type = ToolType.Sprayer }, null, null),
                new Item("wb:drill", "Drill", "drill.png", 1, null, new ToolDefinition { Type = ToolType.Drill }, null, null),
            ]);
            var paints = new FakePaintDatabase();
            var textures = new StubTextureDatabase();
            var shaders = new StubShaderDatabase();
            var icons = new ItemIconDatabase(NullLogger<ItemIconDatabase>.Instance, new StubFileParseService(), textures, items);

            var motionProcessor = new ClientPlayerMotionProcessor(
                _input,
                new FakeWindowContext(),
                new FakePhysics(),
                new EventInvoker<PlayerMovedEvent>([])
            );
            var interactionState = new InteractionState(motionProcessor);
            _playerData = new PlayerData(items);
            var ecsContext = new TestEcsContext(_world);
            var localization = new HintLocalization();

            int player = _world.DataStore.Alloc();
            _world.DataStore.AddOrUpdate(player, new PlayerComponent());
            _world.DataStore.AddOrUpdate(player, new InventoryComponent(45));
            _world.DataStore.AddOrUpdate(player, new EquipmentComponent { ActiveInventorySlot = 0 });
            _world.DataStore.AddOrUpdate(player, new PendingInventoryComponent());

            var orientationSelector = new OrientationSelector(Shortcuts, textures, shaders, interactionState, _playerData, bricks, ecsContext, localization);
            var shapeSelector = new ShapeSelector(interactionState, Shortcuts, textures, shaders, _playerData, bricks, ecsContext, localization);
            var colorSelector = new PaintColorSelector(interactionState, Shortcuts, shaders, _playerData, paints, ecsContext, localization);
            var textureSelector = new PaintTextureSelector(interactionState, Shortcuts, textures, shaders, _playerData, paints, ecsContext, localization);

            _inventory = new Inventory(
                Shortcuts,
                items,
                bricks,
                new StubLocalizedTagsDatabase(),
                icons,
                _playerData,
                ecsContext,
                interactionState,
                _input,
                localization,
                new DebugSettings()
            );

            _skillsLayer = new SkillsLayer(
                Shortcuts,
                interactionState,
                new StubSkillDatabase(),
                new SkillIconDatabase(NullLogger<SkillIconDatabase>.Instance, new Shader("ui", []), textures),
                new ActiveCharacterSave(),
                localization
            );

            for (var i = 0; i < Hotbar.SLOT_COUNT; i++)
            {
                GameShortcuts.RegisterOrAddHandler(Shortcuts, GameShortcuts.CreateSlot(i));
            }

            _controlHints = new ControlHints(
                orientationSelector,
                shapeSelector,
                colorSelector,
                textureSelector,
                _inventory,
                _playerData,
                ecsContext,
                localization,
                new GameplayConfig(),
                Shortcuts,
                skillsLayer: _skillsLayer
            );

            _ui = new UIBuilder<Material>(1280, 720, new StubTextEngine(), new UIController());
        }

        public void RegisterLateShortcuts()
        {
            Shortcuts.RegisterShortcut(GameShortcuts.CreatePickBrick());
            Shortcuts.RegisterShortcut(new Shortcut
            {
                Name = "Snap Placement",
                Category = "Interaction",
                Key = Key.Shift,
                IgnoreUnboundModifiers = true,
                IsEnabled = Shortcut.DefaultEnabled,
            });
            Shortcuts.RegisterShortcut(new Shortcut("Feedback form", "UI", ShortcutModifiers.None, Key.F8, Shortcut.DefaultEnabled, () => { }));
        }

        public void EquipItem(string itemId)
        {
            _playerData.MutateInventory(_world.DataStore, (ref InventoryComponent inventory) =>
            {
                inventory.Contents![0] = new ItemData(itemId, 1, 64);
            });
        }

        public void OpenInventory()
        {
            if (!_inventory.IsVisible())
            {
                Key key = Shortcuts.GetBinding(GameShortcuts.INVENTORY).Value.Key;
                _input.KeyPressed?.Invoke(_input, new KeyEventArgs(key));
                _input.KeyReleased?.Invoke(_input, new KeyEventArgs(key));
            }
        }

        public void OpenSkills()
        {
            _skillsLayer.Open();
        }

        public List<string> RenderTexts()
        {
            _controlHints.RenderUI(0d, _ui);
            List<RenderCommand<Material>> commands = _ui.Build(0d);
            var texts = new List<string>();
            for (var i = 0; i < commands.Count; i++)
            {
                if (!string.IsNullOrEmpty(commands[i].Text))
                {
                    texts.Add(commands[i].Text!);
                }
            }

            return texts;
        }
    }

    private sealed class HintLocalization : ILocalization
    {
        public string? GetString(string value)
        {
            return value switch
            {
                "ui.hint.brick.changeShape" => "Change shape",
                "ui.hint.brick.changeOrientation" => "Change orientation",
                "ui.hint.brick.snap" => "Snap brick",
                "ui.hint.brick.pick" => "Pick brick",
                "ui.hint.blueprint.place" => "Place",
                "ui.hint.region.rotate" => "Rotate",
                "ui.hint.region.mirror" => "Mirror",
                "ui.hint.region.embed" => "Embed",
                "ui.hint.region.distance" => "Distance",
                "ui.hint.region.overwriteAir" => "Overwrite air",
                "ui.hint.region.library" => "Library",
                "ui.hint.sprayer.changeColor" => "Change color",
                "ui.hint.drill.changeTexture" => "Change texture",
                "ui.hint.inventory.toggle" => "Inventory",
                "ui.hint.inventory.move" => "Move / stack",
                "ui.hint.inventory.split" => "Split half",
                "ui.hint.inventory.quickMove" => "Quick move",
                "ui.hint.inventory.hotbar" => "Move to hotbar",
                "ui.hint.inventory.close" => "Close",
                "ui.hint.skill.toggle" => "Skills",
                "ui.hint.skill.close" => "Close",
                "ui.hint.feedback.send" => "Send feedback",
                _ => value,
            };
        }

        public string? GetString(string value, string cultureName) => GetString(value);

        public string? GetString(string value, CultureInfo cultureInfo) => GetString(value);
    }

    private sealed class StubShaderDatabase : IAssetDatabase<Shader>
    {
        public Result<Shader> Get(string id) => Result<Shader>.FromSuccess(null!);
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

    private sealed class FakePaintDatabase : IPaintDatabase
    {
        public IReadOnlyList<Paint> ColorPaints { get; } =
        [
            Paint.CreateColor("wb:red", "Red", new System.Numerics.Vector3(1f, 0f, 0f)),
        ];

        public IReadOnlyList<Paint> TexturePaints { get; } =
        [
            Paint.CreateTexture("wb:smooth", "Smooth", BrickAlpha.Opaque, false, ["stone"]),
        ];

        public Result<Paint> Get(string id) => Result<Paint>.FromFailure("Unknown paint.");

        public Result<Paint> Get(ushort id) => Result<Paint>.FromFailure("Unknown paint.");

        public List<Paint> Get(Func<Paint, bool> predicate) => [];
    }
}
