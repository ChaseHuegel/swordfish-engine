#nullable enable
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Input;
using Swordfish.Library.Configuration;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Shortcuts;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Covers TOML persistence and parsing for <see cref="KeybindingSettings"/>.</summary>
public class KeybindingSettingsTests
{
    [Fact]
    public void RoundTripPreservesSlashAndSpaceShortcutIds()
    {
        var settings = new KeybindingSettings();
        settings.SetOverrides(new Dictionary<string, ShortcutBinding>
        {
            [GameShortcuts.MOVE_FORWARD] = new(Key.UpArrow),
            [GameShortcuts.PRIMARY_ACTION] = new(MouseButton.Right),
            [GameShortcuts.TOGGLE_UI_DEBUG] = new(Key.F4, ShortcutModifiers.Control | ShortcutModifiers.Shift),
            [GameShortcuts.ALT_TOGGLE_FULLSCREEN] = new(Key.F10, ShortcutModifiers.Alt),
        });

        KeybindingSettings loaded = Toml<KeybindingSettings>.FromString(settings.ToString());
        Dictionary<string, ShortcutBinding> parsed = loaded.ParseBindings(NullLogger.Instance);

        Assert.Equal(4, parsed.Count);
        Assert.Equal(new ShortcutBinding(Key.UpArrow), parsed[GameShortcuts.MOVE_FORWARD]);
        Assert.Equal(new ShortcutBinding(MouseButton.Right), parsed[GameShortcuts.PRIMARY_ACTION]);
        Assert.Equal(
            new ShortcutBinding(Key.F4, ShortcutModifiers.Control | ShortcutModifiers.Shift),
            parsed[GameShortcuts.TOGGLE_UI_DEBUG]
        );
        Assert.Equal(new ShortcutBinding(Key.F10, ShortcutModifiers.Alt), parsed[GameShortcuts.ALT_TOGGLE_FULLSCREEN]);
    }

    [Fact]
    public void ParseBindingsSkipsMalformedAndEmptyEntries()
    {
        var settings = new KeybindingSettings();
        settings.Bindings[GameShortcuts.MOVE_FORWARD] = "W";
        settings.Bindings[GameShortcuts.QUICKSAVE] = "NotAKey";
        settings.Bindings[GameShortcuts.PICK_BRICK] = "Mouse.99";
        settings.Bindings[GameShortcuts.INVENTORY] = "";

        Dictionary<string, ShortcutBinding> parsed = settings.ParseBindings(NullLogger.Instance);

        Assert.Single(parsed);
        Assert.Equal(new ShortcutBinding(Key.W), parsed[GameShortcuts.MOVE_FORWARD]);
    }

    [Fact]
    public void SetOverridesSerializesUnboundEntriesAndOrdersKeysDeterministically()
    {
        var input = new Swordfish.Tests.FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);
        shortcuts.RegisterShortcut(GameShortcuts.CreateHeld("Movement", "Move forward", Key.W));
        shortcuts.RegisterShortcut(GameShortcuts.CreatePrimaryAction());

        shortcuts.Rebind(GameShortcuts.MOVE_FORWARD, new ShortcutBinding(Key.UpArrow));
        shortcuts.Rebind(GameShortcuts.PRIMARY_ACTION, ShortcutBinding.Unbound);

        var settings = new KeybindingSettings();
        settings.SetOverrides(shortcuts.GetOverrides());

        Assert.Equal(2, settings.Bindings.Count);
        Assert.Equal(
            [GameShortcuts.PRIMARY_ACTION, GameShortcuts.MOVE_FORWARD],
            new List<string>(settings.Bindings.Keys)
        );
        Assert.Equal(KeybindingSettings.UNBOUND_TOKEN, settings.Bindings[GameShortcuts.PRIMARY_ACTION]);
        Assert.Equal("UpArrow", settings.Bindings[GameShortcuts.MOVE_FORWARD]);

        KeybindingSettings loaded = Toml<KeybindingSettings>.FromString(settings.ToString());
        loaded.Bindings[GameShortcuts.QUICKSAVE] = " unbound ";
        Dictionary<string, ShortcutBinding> parsed = loaded.ParseBindings(NullLogger.Instance);

        Assert.Equal(3, parsed.Count);
        Assert.Equal(ShortcutBinding.Unbound, parsed[GameShortcuts.PRIMARY_ACTION]);
        Assert.Equal(new ShortcutBinding(Key.UpArrow), parsed[GameShortcuts.MOVE_FORWARD]);
        Assert.Equal(ShortcutBinding.Unbound, parsed[GameShortcuts.QUICKSAVE]);
    }

    [Fact]
    public void ApplyOverridesFromSettingsUpdatesAndUnbindsLiveShortcutService()
    {
        var saved = new KeybindingSettings();
        saved.Bindings[GameShortcuts.MOVE_FORWARD] = "UpArrow";
        saved.Bindings[GameShortcuts.PRIMARY_ACTION] = KeybindingSettings.UNBOUND_TOKEN;
        saved.Bindings[GameShortcuts.MOVE_BACKWARD] = KeybindingSettings.UNBOUND_TOKEN;
        saved.Bindings["General/Go back"] = KeybindingSettings.UNBOUND_TOKEN;

        KeybindingSettings loaded = Toml<KeybindingSettings>.FromString(saved.ToString());
        var input = new Swordfish.Tests.FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);

        shortcuts.RegisterShortcut(GameShortcuts.CreateHeld("Movement", "Move backward", Key.S));
        shortcuts.RegisterShortcut(new Shortcut
        {
            Category = "General",
            Name = "Go back",
            Key = Key.Esc,
            Fixed = true,
            IsEnabled = Shortcut.DefaultEnabled,
        });
        shortcuts.ApplyOverrides(loaded.ParseBindings(NullLogger.Instance));
        shortcuts.RegisterShortcut(GameShortcuts.CreateHeld("Movement", "Move forward", Key.W));
        shortcuts.RegisterShortcut(GameShortcuts.CreatePrimaryAction());

        Assert.Equal(new ShortcutBinding(Key.UpArrow), shortcuts.GetBinding(GameShortcuts.MOVE_FORWARD).Value);
        Assert.Equal(ShortcutBinding.Unbound, shortcuts.GetBinding(GameShortcuts.PRIMARY_ACTION).Value);
        Assert.Equal(ShortcutBinding.Unbound, shortcuts.GetBinding(GameShortcuts.MOVE_BACKWARD).Value);
        Assert.Equal(new ShortcutBinding(Key.Esc), shortcuts.GetBinding("General/Go back").Value);
    }
}
