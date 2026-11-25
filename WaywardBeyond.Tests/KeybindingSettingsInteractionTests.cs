#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Reef;
using Reef.UI;
using Shoal.CommandLine;
using Swordfish.Graphics;
using Swordfish.Input;
using Swordfish.Library.IO;
using Swordfish.Settings;
using Swordfish.Tests;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.UI.Layers.Menus;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using WaywardBeyond.Client.UI.Settings;
using WaywardBeyond.Config;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Covers the settings keybindings category: display, capture, conflicts, unbound bindings, reset, and apply.</summary>
public class KeybindingSettingsInteractionTests
{
    private static readonly Vector4 DANGER_BORDER = new(224f / 255f, 79f / 255f, 122f / 255f, 1f);
    private static readonly Vector4 ACCENT_BORDER = new(75f / 255f, 227f / 255f, 227f / 255f, 1f);
    private static readonly Vector4 PRIMARY_BORDER = new(242f / 255f, 153f / 255f, 74f / 255f, 1f);

    [Fact]
    public void KeybindingsCategoryListsAllSectionsWithoutFalseToolConflicts()
    {
        var harness = new Harness();
        harness.OpenKeybindings();

        Assert.True(harness.HasText("ui.settings.section.movement"));
        Assert.True(harness.HasText("ui.settings.section.interaction"));
        Assert.True(harness.HasText("ui.settings.section.inventory"));
        Assert.True(harness.HasText("ui.settings.keybind.moveForward"));
        Assert.True(harness.HasText("ui.settings.keybind.inventoryMove"));
        Assert.True(harness.HasText("ui.settings.keybind.inventorySplit"));
        Assert.True(harness.HasText("ui.settings.keybind.inventoryQuickMove"));
        Assert.False(harness.HasText("ui.settings.status.keybindConflict"));
        Assert.NotEqual(DANGER_BORDER, harness.FindCommand("Settings.Keybind.Interaction/Change shape.bind").Border.Color);

        harness.ScrollRowsToBottom();
        Assert.True(harness.HasText("ui.settings.section.general"));
        Assert.True(harness.HasText("ui.settings.keybind.back"));
        Assert.NotEqual(DANGER_BORDER, harness.FindCommand($"Settings.Keybind.{GameShortcuts.BACK}.bind").Border.Color);
        Assert.True(harness.HasText("ui.settings.keybind.openChat"));
        Assert.True(harness.HasText("ui.settings.keybind.sendChat"));
        Assert.NotEqual(DANGER_BORDER, harness.FindCommand($"Settings.Keybind.{GameShortcuts.OPEN_CHAT}.bind").Border.Color);
        Assert.NotEqual(DANGER_BORDER, harness.FindCommand($"Settings.Keybind.{GameShortcuts.SEND_CHAT}.bind").Border.Color);
        Assert.True(harness.HasText("ui.settings.section.developer"));
        Assert.True(harness.HasText("ui.settings.keybind.toggleDebug"));
    }

    [Fact]
    public void CaptureSupportsKeyboardChordsMouseButtonsAndEscCancel()
    {
        var harness = new Harness();
        harness.OpenKeybindings();

        string forwardBind = "Settings.Keybind.Movement/Move forward.bind";
        harness.Click(harness.Find(forwardBind));
        harness.Frame();
        Assert.True(harness.HasText("ui.settings.keybind.listening"));
        Assert.True(harness.HasText("ui.settings.status.keybindListening"));
        Assert.Equal(ACCENT_BORDER, harness.FindCommand(forwardBind).Border.Color);

        //  Esc cancels capture without dropping the draft or leaving the settings page.
        harness.PressKey(Key.Esc);
        harness.Frame();
        Assert.False(harness.HasText("ui.settings.keybind.listening"));
        Assert.Equal(0, harness.EscGoBackCount);
        Assert.Equal(new ShortcutBinding(Key.W), harness.Shortcuts.GetBinding(GameShortcuts.MOVE_FORWARD).Value);

        //  Capture Control+UpArrow.
        harness.Click(harness.Find(forwardBind));
        harness.Frame();
        harness.HoldKey(Key.Control);
        harness.PressKey(Key.UpArrow);
        harness.ReleaseKey(Key.Control);
        harness.Frame();
        Assert.True(harness.HasText("Ctrl+Up"));
        Assert.Equal(new ShortcutBinding(Key.W), harness.Shortcuts.GetBinding(GameShortcuts.MOVE_FORWARD).Value);

        //  Capture LMB directly on the bind button and verify the click does not re-enter capture mode.
        string primaryBind = "Settings.Keybind.Interaction/Primary action.bind";
        harness.Click(harness.Find(primaryBind));
        harness.Frame();
        Assert.True(harness.Shortcuts.IsCapturing);

        IntRect primaryRect = harness.Find(primaryBind);
        int clickX = primaryRect.Left + primaryRect.Size.X / 2;
        int clickY = primaryRect.Top + primaryRect.Size.Y / 2;
        harness.SimulateMouseDownAt(clickX, clickY, MouseButton.Left);
        harness.Frame();
        harness.SimulateMouseUpAt(clickX, clickY, MouseButton.Left);
        harness.Frame();

        Assert.False(harness.Shortcuts.IsCapturing);
        Assert.False(harness.HasText("ui.settings.keybind.listening"));
        Assert.True(harness.HasText("LMB"));
    }

    [Fact]
    public void GlobalConflictsBlockApplyAndPreserveLiveBindings()
    {
        var harness = new Harness();
        harness.OpenKeybindings();

        string forwardBind = "Settings.Keybind.Movement/Move forward.bind";
        harness.Click(harness.Find(forwardBind));
        harness.Frame();
        harness.PressKey(Key.F3);
        harness.Frame();

        Assert.True(harness.HasText("ui.settings.status.keybindConflict"));
        Assert.Equal(DANGER_BORDER, harness.FindCommand(forwardBind).Border.Color);
        harness.ScrollRowsToBottom();
        Assert.Equal(DANGER_BORDER, harness.FindCommand("Settings.Keybind.Developer/Toggle debug.bind").Border.Color);
        Assert.NotEqual(PRIMARY_BORDER, harness.FindCommand("Settings.Apply").Border.Color);

        harness.Click(harness.Find("Settings.Apply"));
        harness.Frame();
        Assert.Equal(new ShortcutBinding(Key.W), harness.Shortcuts.GetBinding(GameShortcuts.MOVE_FORWARD).Value);

        //  Binding a non-tool action to R conflicts with the tool radial trio.
        harness.ScrollRowsToTop();
        string orientBind = "Settings.Keybind.Interaction/Change orientation.bind";
        harness.Click(harness.Find(orientBind));
        harness.Frame();
        harness.PressKey(Key.R);
        harness.Frame();
        Assert.Equal(DANGER_BORDER, harness.FindCommand(orientBind).Border.Color);
        Assert.Equal(DANGER_BORDER, harness.FindCommand("Settings.Keybind.Interaction/Change shape.bind").Border.Color);
    }

    [Fact]
    public void ClearingBindingsAllowsSavingAsUnboundAndResetRestoresDefaults()
    {
        var harness = new Harness();
        harness.OpenKeybindings();

        string forwardBind = "Settings.Keybind.Movement/Move forward.bind";
        string forwardClear = "Settings.Keybind.Movement/Move forward.clear";
        string forwardReset = "Settings.Keybind.Movement/Move forward.reset";
        string backwardBind = "Settings.Keybind.Movement/Move backward.bind";
        string backwardClear = "Settings.Keybind.Movement/Move backward.clear";

        //  Clear two bindings and verify multiple unbound rows do not conflict and allow apply.
        harness.Click(harness.Find(forwardClear));
        harness.Frame();
        harness.Click(harness.Find(backwardClear));
        harness.Frame();

        Assert.True(harness.HasText("ui.settings.keybind.unbound"));
        Assert.False(harness.HasText("ui.settings.status.keybindConflict"));
        Assert.True(harness.HasText("ui.settings.status.unsaved"));
        Assert.Equal(DANGER_BORDER, harness.FindCommand(forwardBind).Border.Color);
        Assert.Equal(DANGER_BORDER, harness.FindCommand(backwardBind).Border.Color);
        Assert.Equal(PRIMARY_BORDER, harness.FindCommand("Settings.Apply").Border.Color);

        harness.Click(harness.Find("Settings.Apply"));
        harness.Frame();
        Assert.True(harness.HasText("ui.settings.status.saved"));
        Assert.Equal(ShortcutBinding.Unbound, harness.Shortcuts.GetBinding(GameShortcuts.MOVE_FORWARD).Value);
        Assert.Equal(ShortcutBinding.Unbound, harness.Shortcuts.GetBinding(GameShortcuts.MOVE_BACKWARD).Value);
        Assert.Equal(KeybindingSettings.UNBOUND_TOKEN, harness.KeybindingSettings.Bindings[GameShortcuts.MOVE_FORWARD]);
        Assert.Equal(KeybindingSettings.UNBOUND_TOKEN, harness.KeybindingSettings.Bindings[GameShortcuts.MOVE_BACKWARD]);

        //  Per-action reset restores Move forward, leaving Move backward unbound after apply.
        harness.Click(harness.Find(forwardReset));
        harness.Frame();
        Assert.True(harness.HasText("ui.settings.status.unsaved"));
        Assert.Equal(PRIMARY_BORDER, harness.FindCommand("Settings.Apply").Border.Color);

        harness.Click(harness.Find("Settings.Apply"));
        harness.Frame();
        Assert.Equal(new ShortcutBinding(Key.W), harness.Shortcuts.GetBinding(GameShortcuts.MOVE_FORWARD).Value);
        Assert.Equal(ShortcutBinding.Unbound, harness.Shortcuts.GetBinding(GameShortcuts.MOVE_BACKWARD).Value);
        Assert.False(harness.KeybindingSettings.Bindings.ContainsKey(GameShortcuts.MOVE_FORWARD));
        Assert.Equal(KeybindingSettings.UNBOUND_TOKEN, harness.KeybindingSettings.Bindings[GameShortcuts.MOVE_BACKWARD]);

        //  Category reset restores all remaining unbound entries after apply.
        harness.ScrollRowsToBottom();
        harness.Click(harness.Find("Settings.Reset.keybindings"));
        harness.Frame();
        Assert.True(harness.HasText("ui.settings.status.unsaved"));

        harness.Click(harness.Find("Settings.Apply"));
        harness.Frame();
        Assert.Equal(new ShortcutBinding(Key.S), harness.Shortcuts.GetBinding(GameShortcuts.MOVE_BACKWARD).Value);
        Assert.Empty(harness.KeybindingSettings.Bindings);

        //  Reopening settings with an already-unbound override retains danger styling without conflict status.
        var unboundHarness = new Harness(new Dictionary<string, ShortcutBinding>
        {
            [GameShortcuts.MOVE_FORWARD] = ShortcutBinding.Unbound,
        });
        unboundHarness.OpenKeybindings();

        Assert.True(unboundHarness.HasText("ui.settings.keybind.unbound"));
        Assert.False(unboundHarness.HasText("ui.settings.status.unsaved"));
        Assert.False(unboundHarness.HasText("ui.settings.status.keybindConflict"));
        Assert.Equal(DANGER_BORDER, unboundHarness.FindCommand(forwardBind).Border.Color);
        Assert.NotEqual(PRIMARY_BORDER, unboundHarness.FindCommand("Settings.Apply").Border.Color);

        unboundHarness.Click(unboundHarness.Find(forwardReset));
        unboundHarness.Frame();
        Assert.True(unboundHarness.HasText("ui.settings.status.unsaved"));
        unboundHarness.Click(unboundHarness.Find("Settings.Apply"));
        unboundHarness.Frame();
        Assert.Equal(new ShortcutBinding(Key.W), unboundHarness.Shortcuts.GetBinding(GameShortcuts.MOVE_FORWARD).Value);
        Assert.Empty(unboundHarness.KeybindingSettings.Bindings);
    }

    [Fact]
    public void ApplyCommitsReboundShortcutsAndUpdatesKeybindingSettings()
    {
        var harness = new Harness();
        harness.OpenKeybindings();

        harness.Click(harness.Find("Settings.Keybind.Movement/Move forward.bind"));
        harness.Frame();
        harness.PressKey(Key.UpArrow);
        harness.Frame();

        Assert.Equal(PRIMARY_BORDER, harness.FindCommand("Settings.Apply").Border.Color);
        harness.Click(harness.Find("Settings.Apply"));
        harness.Frame();

        Assert.Equal(new ShortcutBinding(Key.UpArrow), harness.Shortcuts.GetBinding(GameShortcuts.MOVE_FORWARD).Value);
        Assert.Equal("UpArrow", harness.KeybindingSettings.Bindings[GameShortcuts.MOVE_FORWARD]);
        Assert.True(harness.HasText("ui.settings.status.saved"));
    }

    [Fact]
    public void FixedBindingOverrideFlagsConflictAndBlocksApplyUntilReset()
    {
        var fixedLock = new Shortcut
        {
            Category = "General",
            Name = "System lock",
            Key = Key.Pause,
            Fixed = true,
            IsEnabled = Shortcut.DefaultEnabled,
        };
        var harness = new Harness(
            initialOverrides: new Dictionary<string, ShortcutBinding>
            {
                [GameShortcuts.MOVE_FORWARD] = new(Key.Pause),
            },
            extraShortcuts: [fixedLock]
        );
        harness.OpenKeybindings();

        string forwardBind = "Settings.Keybind.Movement/Move forward.bind";
        Assert.True(harness.HasText("ui.settings.status.keybindConflict"));
        Assert.Equal(DANGER_BORDER, harness.FindCommand(forwardBind).Border.Color);
        Assert.NotEqual(PRIMARY_BORDER, harness.FindCommand("Settings.Apply").Border.Color);

        harness.Click(harness.Find("Settings.Apply"));
        harness.Frame();
        Assert.Equal(new ShortcutBinding(Key.Pause), harness.Shortcuts.GetBinding(GameShortcuts.MOVE_FORWARD).Value);

        harness.Click(harness.Find("Settings.Keybind.Movement/Move forward.reset"));
        harness.Frame();
        Assert.False(harness.HasText("ui.settings.status.keybindConflict"));
        Assert.Equal(PRIMARY_BORDER, harness.FindCommand("Settings.Apply").Border.Color);

        harness.Click(harness.Find("Settings.Apply"));
        harness.Frame();
        Assert.Equal(new ShortcutBinding(Key.W), harness.Shortcuts.GetBinding(GameShortcuts.MOVE_FORWARD).Value);
    }

    [Fact]
    public void InventoryScopeIsolatesWorldActionsAndDetectsOverlapWithinInventoryOrGlobal()
    {
        var harness = new Harness();
        harness.OpenKeybindings();
        harness.ScrollRowsToBottom();

        string quickMoveBind = $"Settings.Keybind.{GameShortcuts.INVENTORY_QUICK_MOVE}.bind";
        string splitBind = $"Settings.Keybind.{GameShortcuts.INVENTORY_SPLIT_STACK}.bind";

        //  World W and Inventory Q/W can overlap safely because inventory blocks world controls.
        harness.Click(harness.Find(quickMoveBind));
        harness.Frame();
        harness.PressKey(Key.W);
        harness.Frame();
        Assert.False(harness.HasText("ui.settings.status.keybindConflict"));
        Assert.Equal(PRIMARY_BORDER, harness.FindCommand("Settings.Apply").Border.Color);

        //  Matching another Inventory action (RMB) flags both entries and blocks Apply.
        harness.Click(harness.Find(quickMoveBind));
        harness.Frame();
        harness.SimulateMouseClick(MouseButton.Right);
        harness.Frame();
        Assert.True(harness.HasText("ui.settings.status.keybindConflict"));
        Assert.Equal(DANGER_BORDER, harness.FindCommand(quickMoveBind).Border.Color);
        Assert.Equal(DANGER_BORDER, harness.FindCommand(splitBind).Border.Color);
        Assert.NotEqual(PRIMARY_BORDER, harness.FindCommand("Settings.Apply").Border.Color);

        //  Matching a Global action (Slot 1 on D1) also flags a conflict and blocks Apply.
        harness.Click(harness.Find(quickMoveBind));
        harness.Frame();
        harness.PressKey(Key.D1);
        harness.Frame();
        Assert.True(harness.HasText("ui.settings.status.keybindConflict"));
        Assert.Equal(DANGER_BORDER, harness.FindCommand(quickMoveBind).Border.Color);
        Assert.NotEqual(PRIMARY_BORDER, harness.FindCommand("Settings.Apply").Border.Color);

        //  Binding to an unused key Q clears conflicts and commits cleanly.
        harness.Click(harness.Find(quickMoveBind));
        harness.Frame();
        harness.PressKey(Key.Q);
        harness.Frame();
        Assert.False(harness.HasText("ui.settings.status.keybindConflict"));
        harness.Click(harness.Find("Settings.Apply"));
        harness.Frame();
        Assert.Equal(new ShortcutBinding(Key.Q), harness.Shortcuts.GetBinding(GameShortcuts.INVENTORY_QUICK_MOVE).Value);
    }

    [Fact]
    public void ChatScopeAllowsOpenAndSendOnEnterAndDetectsGlobalAndFixedConflicts()
    {
        var fixedLock = new Shortcut
        {
            Category = "General",
            Name = "System lock",
            Key = Key.Pause,
            Fixed = true,
            IsEnabled = Shortcut.DefaultEnabled,
        };
        var harness = new Harness(extraShortcuts: [fixedLock]);
        harness.OpenKeybindings();
        harness.ScrollRowsToBottom();

        string backBind = $"Settings.Keybind.{GameShortcuts.BACK}.bind";
        string backReset = $"Settings.Keybind.{GameShortcuts.BACK}.reset";
        string openChatBind = $"Settings.Keybind.{GameShortcuts.OPEN_CHAT}.bind";
        string sendChatBind = $"Settings.Keybind.{GameShortcuts.SEND_CHAT}.bind";
        string sendChatReset = $"Settings.Keybind.{GameShortcuts.SEND_CHAT}.reset";

        //  Default Enter on both Open chat (World) and Send chat (Chat) does not self-conflict.
        Assert.False(harness.HasText("ui.settings.status.keybindConflict"));
        Assert.NotEqual(DANGER_BORDER, harness.FindCommand(openChatBind).Border.Color);
        Assert.NotEqual(DANGER_BORDER, harness.FindCommand(sendChatBind).Border.Color);

        //  Chat scope is isolated from both World (W) and Inventory (LMB).
        harness.Click(harness.Find(sendChatBind));
        harness.Frame();
        harness.PressKey(Key.W);
        harness.Frame();
        Assert.False(harness.HasText("ui.settings.status.keybindConflict"));
        Assert.Equal(PRIMARY_BORDER, harness.FindCommand("Settings.Apply").Border.Color);

        harness.Click(harness.Find(sendChatBind));
        harness.Frame();
        harness.SimulateMouseClick(MouseButton.Left);
        harness.Frame();
        Assert.False(harness.HasText("ui.settings.status.keybindConflict"));

        //  Matching a Global action (Back / close or F3) or Fixed binding (Pause) flags a conflict and blocks Apply.
        harness.Click(harness.Find(backBind));
        harness.Frame();
        harness.PressKey(Key.Backspace);
        harness.Frame();
        harness.Click(harness.Find(sendChatBind));
        harness.Frame();
        harness.PressKey(Key.Backspace);
        harness.Frame();
        Assert.True(harness.HasText("ui.settings.status.keybindConflict"));
        Assert.Equal(DANGER_BORDER, harness.FindCommand(backBind).Border.Color);
        Assert.Equal(DANGER_BORDER, harness.FindCommand(sendChatBind).Border.Color);
        Assert.NotEqual(PRIMARY_BORDER, harness.FindCommand("Settings.Apply").Border.Color);

        harness.Click(harness.Find(backReset));
        harness.Frame();
        Assert.False(harness.HasText("ui.settings.status.keybindConflict"));

        harness.Click(harness.Find(sendChatBind));
        harness.Frame();
        harness.PressKey(Key.Pause);
        harness.Frame();
        Assert.True(harness.HasText("ui.settings.status.keybindConflict"));
        Assert.Equal(DANGER_BORDER, harness.FindCommand(sendChatBind).Border.Color);

        //  Open chat (World scope) conflicts with World (W), while resetting both back to Enter clears conflicts.
        harness.Click(harness.Find(sendChatReset));
        harness.Frame();
        harness.Click(harness.Find(openChatBind));
        harness.Frame();
        harness.PressKey(Key.W);
        harness.Frame();
        Assert.True(harness.HasText("ui.settings.status.keybindConflict"));
        Assert.Equal(DANGER_BORDER, harness.FindCommand(openChatBind).Border.Color);

        harness.Click(harness.Find($"Settings.Keybind.{GameShortcuts.OPEN_CHAT}.reset"));
        harness.Frame();
        Assert.False(harness.HasText("ui.settings.status.keybindConflict"));

        //  Rebinding Back / close and Send chat commits cleanly without affecting Open chat.
        harness.Click(harness.Find(backBind));
        harness.Frame();
        harness.PressKey(Key.Backspace);
        harness.Frame();
        harness.Click(harness.Find(sendChatBind));
        harness.Frame();
        harness.HoldKey(Key.Control);
        harness.PressKey(Key.Enter);
        harness.ReleaseKey(Key.Control);
        harness.Frame();
        harness.Click(harness.Find("Settings.Apply"));
        harness.Frame();

        Assert.Equal(new ShortcutBinding(Key.Backspace), harness.Shortcuts.GetBinding(GameShortcuts.BACK).Value);
        Assert.Equal("Backspace", harness.KeybindingSettings.Bindings[GameShortcuts.BACK]);
        Assert.Equal(
            new ShortcutBinding(Key.Enter, ShortcutModifiers.Control),
            harness.Shortcuts.GetBinding(GameShortcuts.SEND_CHAT).Value
        );
        Assert.Equal(new ShortcutBinding(Key.Enter), harness.Shortcuts.GetBinding(GameShortcuts.OPEN_CHAT).Value);
    }

    [Fact]
    public void DeveloperSectionIsHiddenWhenDeveloperKeybindingsDisabled()
    {
        var harness = new Harness(showDeveloperKeybindings: false);
        harness.OpenKeybindings();
        harness.ScrollRowsToBottom();

        Assert.True(harness.HasText("ui.settings.section.general"));
        Assert.True(harness.HasText("ui.settings.keybind.back"));
        Assert.False(harness.HasText("ui.settings.section.developer"));
        Assert.False(harness.HasText("ui.settings.keybind.toggleDebug"));

        //  Hidden developer shortcuts do not raise phantom conflicts or block Apply.
        harness.Click(harness.Find($"Settings.Keybind.{GameShortcuts.OPEN_CHAT}.bind"));
        harness.Frame();
        harness.PressKey(Key.F3);
        harness.Frame();
        Assert.False(harness.HasText("ui.settings.status.keybindConflict"));
        Assert.Equal(PRIMARY_BORDER, harness.FindCommand("Settings.Apply").Border.Color);

        harness.Click(harness.Find("Settings.Apply"));
        harness.Frame();
        Assert.Equal(new ShortcutBinding(Key.F3), harness.Shortcuts.GetBinding(GameShortcuts.OPEN_CHAT).Value);

        Assert.True(KeybindingsSettingsCategory.IsDebugFlagPresent(new CommandLineParser().Parse(["--debug"])));
        Assert.True(KeybindingsSettingsCategory.IsDebugFlagPresent(new CommandLineParser().Parse(["-debug"])));
        Assert.False(KeybindingsSettingsCategory.IsDebugFlagPresent(CommandLineArgs.Empty));
        Assert.False(KeybindingsSettingsCategory.IsDebugFlagPresent(new CommandLineParser().Parse(["--portable"])));
    }

    private sealed class Harness
    {
        private readonly UIController _controller = new();
        private readonly UIBuilder<Material> _ui;
        private readonly FakeInputService _input = new();
        private readonly SettingsPage<MenuPage> _page;
        private List<RenderCommand<Material>> _last = [];
        private double _delta;

        public ShortcutService Shortcuts { get; }
        public KeybindingSettings KeybindingSettings { get; } = new();
        public int EscGoBackCount { get; private set; }

        public Harness(
            IReadOnlyDictionary<string, ShortcutBinding>? initialOverrides = null,
            bool showDeveloperKeybindings = true,
            Shortcut[]? extraShortcuts = null
        )
        {
            Shortcuts = new ShortcutService(_input, NullLogger.Instance);
            if (initialOverrides != null)
            {
                Shortcuts.ApplyOverrides(initialOverrides);
            }

            RegisterSampleGameShortcuts(Shortcuts, () => EscGoBackCount++);
            if (extraShortcuts != null)
            {
                for (var i = 0; i < extraShortcuts.Length; i++)
                {
                    Shortcuts.RegisterShortcut(extraShortcuts[i]);
                }
            }

            _ui = new UIBuilder<Material>(1920, 1080, new StubTextEngine(), _controller);
            _page = new SettingsPage<MenuPage>(
                MenuPage.Settings,
                configManager: null!,
                controlSettings: new ControlSettings(),
                keybindingSettings: KeybindingSettings,
                windowSettings: new WindowSettings(),
                renderSettings: new RenderSettings(),
                volumeSettings: new VolumeSettings(),
                playbackDeviceService: new FakePlaybackDeviceService(),
                gameplayConfig: new GameplayConfig(),
                uiSettings: new UISettings(),
                soundEffectService: null!,
                windowContext: new FakeWindowContext(),
                shortcutService: Shortcuts,
                localization: new TestLocalization(),
                showDeveloperKeybindings: showDeveloperKeybindings
            );

            _page.OnEnter();
            Frame();
        }

        public void OpenKeybindings()
        {
            Click(Find("Settings.Category.keybindings"));
            Frame();
        }

        public List<RenderCommand<Material>> Frame()
        {
            _delta += 0.05d;
            _page.RenderPage(0d, _ui, null!);
            _last = _ui.Build(_delta);
            return _last;
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

        public IntRect Find(string id) => FindCommand(id).Rect;

        public bool HasText(string text)
        {
            for (var i = 0; i < _last.Count; i++)
            {
                if (_last[i].Text == text)
                {
                    return true;
                }
            }

            return false;
        }

        public void Click(IntRect target)
        {
            int x = target.Left + target.Size.X / 2;
            int y = target.Top + target.Size.Y / 2;

            _controller.UpdateMouse(x, y, UIController.MouseButtons.None);
            Frame();
            _controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
            Frame();
            _controller.UpdateMouse(x, y, UIController.MouseButtons.None);
            Frame();
        }

        public void ScrollRowsToBottom()
        {
            IntRect listRect = Find("Settings.Rows");
            int trackX = listRect.Right - 3;
            int trackY = listRect.Bottom - 10;
            _controller.UpdateMouse(trackX, trackY, UIController.MouseButtons.Left);
            Frame();
            _controller.UpdateMouse(trackX, trackY, UIController.MouseButtons.None);
            Frame();
            Frame();
        }

        public void ScrollRowsToTop()
        {
            IntRect listRect = Find("Settings.Rows");
            int trackX = listRect.Right - 3;
            int trackY = listRect.Top + 10;
            _controller.UpdateMouse(trackX, trackY, UIController.MouseButtons.Left);
            Frame();
            _controller.UpdateMouse(trackX, trackY, UIController.MouseButtons.None);
            Frame();
            Frame();
        }

        public void SimulateMouseClick(MouseButton button)
        {
            _input.HeldButtons.Add(button);
            _input.Clicked?.Invoke(_input, new ClickedEventArgs(button, Vector2.Zero));
            _input.HeldButtons.Remove(button);
            _input.MouseReleased?.Invoke(_input, new ClickedEventArgs(button, Vector2.Zero));
        }

        public void HoldKey(Key key) => _input.HeldKeys.Add(key);

        public void ReleaseKey(Key key)
        {
            _input.HeldKeys.Remove(key);
            _input.KeyReleased?.Invoke(_input, new KeyEventArgs(key));
        }

        public void PressKey(Key key)
        {
            _input.HeldKeys.Add(key);
            _input.KeyPressed?.Invoke(_input, new KeyEventArgs(key));
            _input.HeldKeys.Remove(key);
            _input.KeyReleased?.Invoke(_input, new KeyEventArgs(key));
        }

        public void SimulateMouseDownAt(int x, int y, MouseButton button)
        {
            _input.HeldButtons.Add(button);
            _input.Clicked?.Invoke(_input, new ClickedEventArgs(button, new Vector2(x, y)));
            _controller.UpdateMouse(x, y, ToUiButtons(button));
        }

        public void SimulateMouseUpAt(int x, int y, MouseButton button)
        {
            _input.HeldButtons.Remove(button);
            _input.MouseReleased?.Invoke(_input, new ClickedEventArgs(button, new Vector2(x, y)));
            _controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        }

        private static UIController.MouseButtons ToUiButtons(MouseButton button)
        {
            return button switch
            {
                MouseButton.Left => UIController.MouseButtons.Left,
                MouseButton.Right => UIController.MouseButtons.Right,
                MouseButton.Middle => UIController.MouseButtons.Middle,
                _ => UIController.MouseButtons.None,
            };
        }

        private static void RegisterSampleGameShortcuts(IShortcutService shortcuts, Action onEscGoBack)
        {
            shortcuts.RegisterShortcut(GameShortcuts.CreateHeld("Movement", "Move forward", Key.W));
            shortcuts.RegisterShortcut(GameShortcuts.CreateHeld("Movement", "Move backward", Key.S));
            shortcuts.RegisterShortcut(GameShortcuts.CreatePrimaryAction());
            shortcuts.RegisterShortcut(GameShortcuts.CreateSecondaryAction());
            shortcuts.RegisterShortcut(new Shortcut
            {
                Category = "Interaction",
                Name = "Change shape",
                Key = Key.R,
                SharedBindingGroup = GameShortcuts.TOOL_RADIAL_GROUP,
                IsEnabled = Shortcut.DefaultEnabled,
            });
            shortcuts.RegisterShortcut(new Shortcut
            {
                Category = "Interaction",
                Name = "Change orientation",
                Key = Key.T,
                IsEnabled = Shortcut.DefaultEnabled,
            });
            shortcuts.RegisterShortcut(new Shortcut
            {
                Category = "Interaction",
                Name = "Select paint color",
                Key = Key.R,
                SharedBindingGroup = GameShortcuts.TOOL_RADIAL_GROUP,
                IsEnabled = Shortcut.DefaultEnabled,
            });
            shortcuts.RegisterShortcut(new Shortcut
            {
                Category = "Interaction",
                Name = "Select paint texture",
                Key = Key.R,
                SharedBindingGroup = GameShortcuts.TOOL_RADIAL_GROUP,
                IsEnabled = Shortcut.DefaultEnabled,
            });
            shortcuts.RegisterShortcut(GameShortcuts.CreateInventoryMoveStack());
            shortcuts.RegisterShortcut(GameShortcuts.CreateInventorySplitStack());
            shortcuts.RegisterShortcut(GameShortcuts.CreateInventoryQuickMove());
            shortcuts.RegisterShortcut(GameShortcuts.CreateSlot(0));
            shortcuts.RegisterShortcut(new Shortcut("Inventory", "UI", ShortcutModifiers.None, Key.Tab, Shortcut.DefaultEnabled, () => { }));
            shortcuts.RegisterShortcut(new Shortcut("Open Chat", "Chat", ShortcutModifiers.None, Key.Enter, Shortcut.DefaultEnabled, () => { }));
            shortcuts.RegisterShortcut(new Shortcut("Send Chat", "Chat", ShortcutModifiers.None, Key.Enter, Shortcut.DefaultEnabled, () => { }));
            shortcuts.RegisterShortcut(new Shortcut("Quicksave", "General", ShortcutModifiers.None, Key.F5, Shortcut.DefaultEnabled, () => { }));
            shortcuts.RegisterShortcut(new Shortcut("Toggle debug", "Developer", ShortcutModifiers.None, Key.F3, Shortcut.DefaultEnabled, () => { }));
            GameShortcuts.RegisterOrAddHandler(shortcuts, GameShortcuts.CreateBack(), Shortcut.DefaultEnabled, onEscGoBack);
        }
    }
}
