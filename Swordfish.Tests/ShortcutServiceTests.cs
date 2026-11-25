using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Input;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Xunit;

namespace Swordfish.Tests;

/// <summary>Covers shortcut matching, rebinding, hold queries, mouse actions, and capture mode.</summary>
public class ShortcutServiceTests
{
    [Fact]
    public void UnmodifiedShortcutIgnoresHeldModifiers()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);
        var invocations = 0;

        shortcuts.RegisterShortcut(new Shortcut(
            "Take screenshot",
            "General",
            ShortcutModifiers.None,
            Key.F12,
            Shortcut.DefaultEnabled,
            () => invocations++
        ));

        input.HeldKeys.Add(Key.Shift);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.F12));
        Assert.Equal(0, invocations);

        input.HeldKeys.Remove(Key.Shift);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.F12));
        Assert.Equal(1, invocations);
    }

    [Fact]
    public void ModifiedShortcutRequiresTheExactModifiers()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);
        var invocations = 0;

        shortcuts.RegisterShortcut(new Shortcut(
            "Reload",
            "General",
            ShortcutModifiers.Control,
            Key.F12,
            Shortcut.DefaultEnabled,
            () => invocations++
        ));

        input.HeldKeys.Add(Key.Shift);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.F12));
        Assert.Equal(0, invocations);

        input.HeldKeys.Add(Key.Control);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.F12));
        Assert.Equal(0, invocations);

        input.HeldKeys.Remove(Key.Shift);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.F12));
        Assert.Equal(1, invocations);
    }

    [Fact]
    public void ModifierKeyShortcutFiresForItsOwnPress()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);
        var invocations = 0;

        shortcuts.RegisterShortcut(new Shortcut(
            "Snap placement",
            "Interaction",
            ShortcutModifiers.None,
            Key.Shift,
            Shortcut.DefaultEnabled,
            () => invocations++
        ));

        input.HeldKeys.Add(Key.Shift);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Shift));
        Assert.Equal(1, invocations);
    }

    [Fact]
    public void RebindingMovesTriggerAndRaisesBindingsChanged()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);
        var invocations = 0;
        var changedCount = 0;
        shortcuts.BindingsChanged += () => changedCount++;

        shortcuts.RegisterShortcut(new Shortcut(
            "Change shape",
            "Interaction",
            ShortcutModifiers.None,
            Key.R,
            Shortcut.DefaultEnabled,
            () => invocations++
        ));

        Result rebindResult = shortcuts.Rebind("Interaction/Change shape", new ShortcutBinding(Key.G, ShortcutModifiers.Control));
        Assert.True(rebindResult.Success);
        Assert.Equal(1, changedCount);

        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.R));
        Assert.Equal(0, invocations);

        input.HeldKeys.Add(Key.Control);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.G));
        Assert.Equal(1, invocations);
        Assert.Equal(new ShortcutBinding(Key.G, ShortcutModifiers.Control), shortcuts.GetBinding("Interaction/Change shape").Value);
    }

    [Fact]
    public void FixedShortcutRejectsRebindAndReset()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);

        shortcuts.RegisterShortcut(new Shortcut
        {
            Name = "Go back",
            Category = "General",
            Key = Key.Esc,
            Fixed = true,
            IsEnabled = Shortcut.DefaultEnabled,
            Action = () => { },
        });

        Assert.False(shortcuts.Rebind("General/Go back", new ShortcutBinding(Key.Q)).Success);
        Assert.False(shortcuts.ResetBinding("General/Go back").Success);
        Assert.Equal(new ShortcutBinding(Key.Esc), shortcuts.GetBinding("General/Go back").Value);

        ShortcutInfo info = Assert.Single(shortcuts.GetShortcuts());
        Assert.False(info.Rebindable);
    }

    [Fact]
    public void OverridesApplyBeforeAndAfterRegistrationAndResetClearsThem()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);

        shortcuts.ApplyOverrides(new Dictionary<string, ShortcutBinding>
        {
            ["General/Quicksave"] = new(Key.F9),
            ["Interaction/Change shape"] = new(Key.R),
        });

        shortcuts.RegisterShortcut(new Shortcut("Quicksave", "General", ShortcutModifiers.None, Key.F5, Shortcut.DefaultEnabled, () => { }));
        shortcuts.RegisterShortcut(new Shortcut("Change shape", "Interaction", ShortcutModifiers.None, Key.R, Shortcut.DefaultEnabled, () => { }));

        Assert.Equal(new ShortcutBinding(Key.F9), shortcuts.GetBinding("General/Quicksave").Value);
        Assert.Single(shortcuts.GetOverrides());
        Assert.Equal(new ShortcutBinding(Key.F9), shortcuts.GetOverrides()["General/Quicksave"]);

        Assert.True(shortcuts.ResetBinding("General/Quicksave").Success);
        Assert.Equal(new ShortcutBinding(Key.F5), shortcuts.GetBinding("General/Quicksave").Value);
        Assert.Empty(shortcuts.GetOverrides());

        shortcuts.Rebind("Interaction/Change shape", new ShortcutBinding(Key.V));
        Assert.Single(shortcuts.GetOverrides());
        shortcuts.ResetAllBindings();
        Assert.Empty(shortcuts.GetOverrides());
        Assert.Equal(new ShortcutBinding(Key.R), shortcuts.GetBinding("Interaction/Change shape").Value);
    }

    [Fact]
    public void MouseShortcutFiresPressAndReleaseAndHonorsIgnoreUnboundModifiers()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);
        var primaryPresses = 0;
        var primaryReleases = 0;
        var shiftClicks = 0;
        var shiftClickEnabled = false;

        shortcuts.RegisterShortcut(new Shortcut
        {
            Name = "Primary action",
            Category = "Interaction",
            MouseButton = MouseButton.Left,
            IgnoreUnboundModifiers = true,
            IsEnabled = Shortcut.DefaultEnabled,
            Action = () => primaryPresses++,
            Released = () => primaryReleases++,
        });

        shortcuts.RegisterShortcut(new Shortcut
        {
            Name = "Shift click",
            Category = "Inventory",
            MouseButton = MouseButton.Left,
            Modifiers = ShortcutModifiers.Shift,
            IsEnabled = () => shiftClickEnabled,
            Action = () => shiftClicks++,
        });

        input.HeldKeys.Add(Key.Shift);
        input.Clicked?.Invoke(input, new ClickedEventArgs(MouseButton.Left, Vector2.Zero));
        Assert.Equal(1, primaryPresses);
        Assert.Equal(0, shiftClicks);

        input.MouseReleased?.Invoke(input, new ClickedEventArgs(MouseButton.Left, Vector2.Zero));
        Assert.Equal(1, primaryReleases);

        shiftClickEnabled = true;
        input.Clicked?.Invoke(input, new ClickedEventArgs(MouseButton.Left, Vector2.Zero));
        Assert.Equal(1, primaryPresses);
        Assert.Equal(1, shiftClicks);

        input.MouseReleased?.Invoke(input, new ClickedEventArgs(MouseButton.Left, Vector2.Zero));
        Assert.Equal(1, primaryReleases);
    }

    [Fact]
    public void IsHeldTracksKeyboardMouseModifiersAndRebinds()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);

        shortcuts.RegisterShortcut(new Shortcut
        {
            Name = "Move forward",
            Category = "Movement",
            Key = Key.W,
            IgnoreUnboundModifiers = true,
            IsEnabled = Shortcut.DefaultEnabled,
        });

        shortcuts.RegisterShortcut(new Shortcut
        {
            Name = "Primary action",
            Category = "Interaction",
            MouseButton = MouseButton.Left,
            IgnoreUnboundModifiers = true,
            IsEnabled = Shortcut.DefaultEnabled,
        });

        Assert.False(shortcuts.IsHeld("Movement/Move forward"));
        input.HeldKeys.Add(Key.W);
        input.HeldKeys.Add(Key.Shift);
        Assert.True(shortcuts.IsHeld("Movement/Move forward"));

        shortcuts.Rebind("Movement/Move forward", new ShortcutBinding(Key.UpArrow, ShortcutModifiers.Control));
        Assert.False(shortcuts.IsHeld("Movement/Move forward"));

        input.HeldKeys.Add(Key.UpArrow);
        Assert.False(shortcuts.IsHeld("Movement/Move forward"));

        input.HeldKeys.Add(Key.Control);
        Assert.True(shortcuts.IsHeld("Movement/Move forward"));

        shortcuts.Rebind("Movement/Move forward", ShortcutBinding.Unbound);
        Assert.False(shortcuts.IsHeld("Movement/Move forward"));

        input.HeldButtons.Add(MouseButton.Left);
        Assert.True(shortcuts.IsHeld("Interaction/Primary action"));
    }

    [Fact]
    public void AddHandlerSharesOneBindingAcrossConsumers()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);
        var hotbarInvocations = 0;
        var inventoryInvocations = 0;
        var inventoryVisible = false;

        shortcuts.RegisterShortcut(new Shortcut
        {
            Name = "Slot 1",
            Category = "Inventory",
            Key = Key.D1,
            IsEnabled = () => !inventoryVisible,
            Action = () => hotbarInvocations++,
        });

        Assert.True(shortcuts.AddHandler(
            "Inventory/Slot 1",
            isEnabled: () => inventoryVisible,
            action: () => inventoryInvocations++
        ));

        Assert.True(shortcuts.Rebind("Inventory/Slot 1", new ShortcutBinding(Key.Numpad1)).Success);

        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Numpad1));
        Assert.Equal(1, hotbarInvocations);
        Assert.Equal(0, inventoryInvocations);

        inventoryVisible = true;
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Numpad1));
        Assert.Equal(1, hotbarInvocations);
        Assert.Equal(1, inventoryInvocations);
    }

    [Fact]
    public void CaptureModeCapturesKeysChordsBareModifiersMouseAndCancelsOnEscWithoutDispatch()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);
        var actionFired = 0;
        var escFired = 0;

        shortcuts.RegisterShortcut(new Shortcut("Quicksave", "General", ShortcutModifiers.None, Key.F5, Shortcut.DefaultEnabled, () => actionFired++));
        shortcuts.RegisterShortcut(new Shortcut("Close", "General", ShortcutModifiers.None, Key.Esc, Shortcut.DefaultEnabled, () => escFired++));

        ShortcutCaptureResult result = default;
        shortcuts.BeginCapture(captured => result = captured);
        Assert.True(shortcuts.IsCapturing);

        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.F5));
        Assert.False(shortcuts.IsCapturing);
        Assert.False(result.Cancelled);
        Assert.Equal(new ShortcutBinding(Key.F5), result.Binding);
        Assert.Equal(0, actionFired);

        shortcuts.BeginCapture(captured => result = captured);
        input.HeldKeys.Add(Key.Alt);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Alt));
        Assert.True(shortcuts.IsCapturing);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Enter));
        Assert.False(shortcuts.IsCapturing);
        Assert.Equal(new ShortcutBinding(Key.Enter, ShortcutModifiers.Alt), result.Binding);
        input.HeldKeys.Remove(Key.Alt);

        shortcuts.BeginCapture(captured => result = captured);
        input.HeldKeys.Add(Key.Shift);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Shift));
        Assert.True(shortcuts.IsCapturing);
        input.HeldKeys.Remove(Key.Shift);
        input.KeyReleased?.Invoke(input, new KeyEventArgs(Key.Shift));
        Assert.False(shortcuts.IsCapturing);
        Assert.Equal(new ShortcutBinding(Key.Shift), result.Binding);

        shortcuts.BeginCapture(captured => result = captured);
        input.Clicked?.Invoke(input, new ClickedEventArgs(MouseButton.Middle, Vector2.Zero));
        Assert.False(shortcuts.IsCapturing);
        Assert.Equal(new ShortcutBinding(MouseButton.Middle), result.Binding);

        shortcuts.BeginCapture(captured => result = captured);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Esc));
        Assert.False(shortcuts.IsCapturing);
        Assert.True(result.Cancelled);
        Assert.Equal(0, escFired);

        shortcuts.BeginCapture(captured => result = captured);
        shortcuts.CancelCapture();
        Assert.False(shortcuts.IsCapturing);
        Assert.True(result.Cancelled);
    }

    [Fact]
    public void RebindingWhilePressedStillTriggersReleaseOnOriginalKey()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);
        var releases = 0;

        shortcuts.RegisterShortcut(new Shortcut
        {
            Name = "Snap",
            Category = "Interaction",
            Key = Key.Shift,
            IsEnabled = Shortcut.DefaultEnabled,
            Action = () => { },
            Released = () => releases++,
        });

        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Shift));
        shortcuts.Rebind("Interaction/Snap", new ShortcutBinding(Key.C));
        input.KeyReleased?.Invoke(input, new KeyEventArgs(Key.Shift));

        Assert.Equal(1, releases);
    }

    [Fact]
    public void IsEnabledEvaluatesOutsideLockAndIsolatesExceptions()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);
        var siblingInvocations = 0;
        ShortcutBinding observedBinding = default;

        shortcuts.RegisterShortcut(new Shortcut
        {
            Name = "Throwing",
            Category = "Test",
            Key = Key.K,
            IsEnabled = () => throw new System.InvalidOperationException("boom"),
            Action = () => { },
        });

        shortcuts.RegisterShortcut(new Shortcut
        {
            Name = "Sibling",
            Category = "Test",
            Key = Key.K,
            IsEnabled = () =>
            {
                observedBinding = shortcuts.GetBinding("Test/Sibling").Value;
                return observedBinding == new ShortcutBinding(Key.K);
            },
            Action = () => siblingInvocations++,
        });

        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.K));

        Assert.Equal(new ShortcutBinding(Key.K), observedBinding);
        Assert.Equal(1, siblingInvocations);
    }

    [Fact]
    public void RegisterShortcutPurgesFixedOverridesAndNotifiesWhenOverrideApplies()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);
        var changedCount = 0;
        shortcuts.BindingsChanged += () => changedCount++;

        shortcuts.ApplyOverrides(new Dictionary<string, ShortcutBinding>
        {
            ["General/Go back"] = new(Key.Q),
            ["General/Quicksave"] = new(Key.F9),
        });
        Assert.Equal(0, changedCount);

        shortcuts.RegisterShortcut(new Shortcut
        {
            Name = "Go back",
            Category = "General",
            Key = Key.Esc,
            Fixed = true,
            IsEnabled = Shortcut.DefaultEnabled,
            Action = () => { },
        });

        Assert.Equal(0, changedCount);
        Assert.Equal(new ShortcutBinding(Key.Esc), shortcuts.GetBinding("General/Go back").Value);
        Assert.False(shortcuts.GetOverrides().ContainsKey("General/Go back"));

        shortcuts.RegisterShortcut(new Shortcut("Quicksave", "General", ShortcutModifiers.None, Key.F5, Shortcut.DefaultEnabled, () => { }));

        Assert.Equal(1, changedCount);
        Assert.Equal(new ShortcutBinding(Key.F9), shortcuts.GetBinding("General/Quicksave").Value);
        Assert.Equal(new ShortcutBinding(Key.F9), shortcuts.GetOverrides()["General/Quicksave"]);
    }

    [Fact]
    public void CaptureModePreservesHeldModifierWhenReleasingAnotherModifier()
    {
        var input = new FakeInputService();
        var shortcuts = new ShortcutService(input, NullLogger.Instance);
        ShortcutCaptureResult result = default;

        shortcuts.BeginCapture(captured => result = captured);
        input.HeldKeys.Add(Key.Control);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Control));
        input.HeldKeys.Add(Key.Shift);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Shift));
        Assert.True(shortcuts.IsCapturing);

        input.HeldKeys.Remove(Key.Shift);
        input.KeyReleased?.Invoke(input, new KeyEventArgs(Key.Shift));

        Assert.False(shortcuts.IsCapturing);
        Assert.False(result.Cancelled);
        Assert.Equal(new ShortcutBinding(Key.Shift, ShortcutModifiers.Control), result.Binding);
        input.HeldKeys.Remove(Key.Control);

        //  Releasing a bare modifier with no other modifier held captures it alone.
        shortcuts.BeginCapture(captured => result = captured);
        input.HeldKeys.Add(Key.Shift);
        input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Shift));
        input.HeldKeys.Remove(Key.Shift);
        input.KeyReleased?.Invoke(input, new KeyEventArgs(Key.Shift));

        Assert.False(shortcuts.IsCapturing);
        Assert.Equal(new ShortcutBinding(Key.Shift), result.Binding);
    }
}
