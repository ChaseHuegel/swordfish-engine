# Input shortcuts

`ShortcutService` turns key presses and mouse clicks into registered shortcut
actions. Each shortcut holds a `ShortcutBinding` with a `Key` or `MouseButton`,
a `ShortcutModifiers` set, and optional press and release handlers.

## Bindings and matching rules

A `ShortcutBinding` (`Swordfish.Library/IO/ShortcutBinding.cs:9`) stores one
keyboard key or one mouse button plus optional Control, Shift, and Alt
modifiers. `ShortcutBinding.Unbound` represents a cleared binding.
`TryParse` (`Swordfish.Library/IO/ShortcutBinding.cs:97`) reads config strings,
and `ToDisplayString` (`Swordfish.Library/IO/ShortcutBinding.cs:157`) formats
UI labels.

By default, a shortcut fires only when the pressed trigger equals the binding
and the held modifiers match exactly. A held modifier blocks a shortcut that
declares none. For example, Shift+F12 does not fire the F12 screenshot shortcut.

A shortcut with `IgnoreUnboundModifiers` set fires even when extra modifiers are
held, as long as no enabled shortcut matches the exact modifier chord. For
example, Shift+LMB fires an unmodified primary action unless an active shortcut
binds Shift+LMB.

The service reads Control, Shift, and Alt hold state from `IInputService` when
the press arrives (`Swordfish/Input/ShortcutService.cs:782`). A modifier key
press does not count itself, so a shortcut bound to Shift alone still fires. The
input service updates hold state before it raises `KeyPressed` or `Clicked`
(`Swordfish/Input/SilkInputService.cs:315`). Candidate collection runs under
the lock in `CollectPressCandidates` at `Swordfish/Input/ShortcutService.cs:602`.

## Press, release, and hold queries

`DispatchPressCandidates` (`Swordfish/Input/ShortcutService.cs:647`) evaluates
each candidate's `IsEnabled` predicate outside the lock and logs any predicate
exception. Only the winning exact or relaxed match list arms `PendingRelease`
and records `PendingBinding`. `ShortcutService` releases the lock before
invoking any callback (`Swordfish/Input/ShortcutService.cs:758`) and logs
exceptions. A later key or mouse release invokes `Released` only when
`PendingRelease` is set for that original trigger
(`Swordfish/Input/ShortcutService.cs:725`). This prevents a release action
without a matching press, including when a shortcut is rebound while held.

`IsHeld` (`Swordfish/Input/ShortcutService.cs:228`) checks whether a shortcut's
current trigger and required modifiers are held without allocating.

## Registration and rebinding

`IShortcutService` (`Swordfish.Library/IO/IShortcutService.cs:9`) keys each
shortcut by `Category/Name` via `RegisterShortcut`
(`Swordfish/Input/ShortcutService.cs:110`) and returns false for duplicates.
When `RegisterShortcut` applies a non-default pending override to a rebindable
shortcut, it raises `BindingsChanged` outside the lock. A null `IsEnabled` means
the shortcut is always enabled. `AddHandler`
(`Swordfish/Input/ShortcutService.cs:169`) links extra handlers to one shared
shortcut id.

`Rebind`, `ResetBinding`, `ResetAllBindings`, `ApplyOverrides`, and
`GetOverrides` manage non-fixed bindings and raise `BindingsChanged` outside the
lock (`Swordfish/Input/ShortcutService.cs:284-424`). Shortcuts with `Fixed`
set reject rebind and reset calls and purge any stale override entries.
`GetShortcuts` returns `ShortcutInfo` snapshots in registration order.

## Capture mode

`BeginCapture` (`Swordfish/Input/ShortcutService.cs:433`) enters single-input
capture mode. While active, `OnKeyPressed` (`:465`), `OnKeyReleased` (`:517`),
and `OnMouseClicked` (`:549`) suppress normal shortcut dispatch. Pressing `Esc`
or calling `CancelCapture` returns a cancelled `ShortcutCaptureResult`. Pressing
a non-modifier key or mouse button captures that input with held modifiers.
Releasing a modifier key without pressing another trigger captures that modifier
together with any other held modifiers.

## Tests

- `Swordfish.Tests/ShortcutServiceTests.cs:12` pins exact modifier matching,
  relaxed modifier precedence, mouse press and release, `IsHeld`, `AddHandler`,
  live rebinding, override application, reset, fixed protection, and capture
  mode.
- `Swordfish.Tests/ShortcutBindingTests.cs:7` pins unbound normalization,
  self-modifier stripping, config serialization round-trips, malformed parse
  rejection, and friendly key display strings.
