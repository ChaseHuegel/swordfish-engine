# Input shortcuts

`ShortcutService` turns key presses into registered shortcut actions. Each
shortcut holds a `Key`, a `ShortcutModifiers` set, and optional press and
release actions.

## Matching rule

A shortcut fires only when the pressed key equals `Shortcut.Key` and the held
modifiers equal `Shortcut.Modifiers`. The match is exact. A held modifier
blocks a shortcut that declares none. For example, Shift+F12 does not fire the
F12 screenshot shortcut.

The service reads the Control, Shift, and Alt hold state from `IInputService`
when the key press arrives. A modifier key press does not count itself, so a
shortcut bound to Shift alone still fires. The input service updates the hold
state before it raises `KeyPressed`, so the modifier check sees the current
state. The check is at `Swordfish/Input/ShortcutService.cs:43`, and the exact
comparison is at `Swordfish/Input/ShortcutService.cs:68`.

## Press and release actions

A shortcut that fires on press sets `PendingRelease`. It then invokes `Action`
and logs any exception. A later key release invokes `Released`, but only when
`PendingRelease` is set and `Released` is not null. This prevents a release
action without a matching press, such as a release after a modifier blocked
the press.

## Registration

`RegisterShortcut` keys each shortcut by `Category/Name` and returns false for
a duplicate. A null `IsEnabled` means the shortcut is always enabled.

## Tests

`Swordfish.Tests/ShortcutServiceTests.cs:9` pins the exact modifier match for
an unmodified shortcut and for a modified shortcut.
