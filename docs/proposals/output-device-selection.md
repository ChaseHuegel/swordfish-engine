# Output Device Selection — Proposal and Development Plan

Status: implemented.

Issue: [ChaseHuegel/swordfish-engine#748](https://github.com/ChaseHuegel/swordfish-engine/issues/748).
Local issue: [0095](../issues/0095-output-device-selection.md).
Milestone: Wayward Beyond — Early Access.

Scope: the engine audio tier (`Swordfish`), the client audio settings
(`WaywardBeyond.Client`), and the docs. No server, networking, or persistence
schema change.

One name per thing: the engine calls the hardware a **playback device**. The
settings page calls it an **output device**. Both names mean the same object.
The UI row stores the engine device name.

## 1. Summary

The engine discovers playback devices and routes each audio channel. It does
not let a caller see the device list or choose a device. `AudioSystem` is
internal, and `AudioChannel.PlaybackDevice` takes a name that a caller cannot
discover.

Wayward Beyond has no output device row in the audio settings category. A
player can use the system default only. A saved choice is impossible.

The change has three parts:

1. A public `IPlaybackDeviceService` in `Swordfish.Audio`. It exposes the
   device names, the system default, the preferred device, the active device,
   a change event, and a setter for the preference.
2. A game output device row in the audio settings category. The row uses the
   shared `Stepper`, lists every device, and starts with `System default`. The
   preference lives in `volume.toml` and applies at startup.
3. A docs pass that records the service contract and the settings behavior.

The engine keeps the preference in memory. The game owns persistence. The
engine stays host-agnostic, so the server and the demo do not write a player
preference.

## 2. Current state before the change

### 2.1 Engine

- `AudioSystem` was internal and owned the device map keyed by
  `DeviceInfo.Name`.
- The system enumerated devices at construction and every 2 seconds.
- `RefreshPlaybackDevices` added and removed devices and resolved one default
  reference. `ResolveDefaultPlaybackDevice` followed `DeviceInfo.IsDefault`.
- A channel with an empty device used the default. A channel with an unknown
  name also fell back to the default.
- `AudioChannel.PlaybackDevice` was the only public device selector.
- `AudioSystem` was registered only as an `IEntitySystem`.

The hotplug path existed. The missing piece was a public read and command
surface. The current contract is in [audio](../specs/audio.md).

### 2.2 Game

- The audio category had one section and four volume sliders.
- The settings page built the categories once on enter.
- `SettingsDraft` copied every editable config value.
- `StepperEntry<T>` captured a static option list at construction.
- `VolumeSettings` persisted audio preferences in `volume.toml`.
- `ConfigManager` saved every config at construction.

The page could not show a device list that changed at runtime. The stepper was
and is the only selection control in the page. The page has no dropdown. The
current behavior is in [settings-ui](../specs/settings-ui.md).

### 2.3 Docs

- `docs/specs/audio.md` recorded discovery, default routing, and hotplug.
- `docs/specs/settings-ui.md` recorded the audio category as four volume rows.
- `docs/specs/config-schemas.md` had no `volume.toml` table.

The docs commit records the final behavior.

## 3. Decisions

### D1 — The game owns the saved preference

The preference lives in `VolumeSettings.OutputDevice` and `volume.toml`. The
engine service holds the preference in memory for one process.

Reasons:

- The value is a player preference. It is not an engine capability.
- The client already saves `volume.toml` through `ConfigManager`.
- The engine service stays reusable by the server, the demo, and tests. Those
  hosts have no player config.
- The engine `AudioSettings` was considered and rejected. It would put a player
  preference in an engine config file, and it would make the engine service a
  writer of host configuration.

`ConfigManager` applies the saved value once at startup. This happens before
`AppEngine.Start` resolves the ECS entry point, because `ConfigManager` is an
`IAutoActivate` (`Shoal/AppEngine.cs:51-53`). The application is one call:

```csharp
playbackDeviceService.SetPreferredPlaybackDevice(volumeSettings.OutputDevice.Get());
```

Resolving the service during auto-activate constructs `AudioSystem` before
`AppEngine.Start`. The audio tier has no window or GL dependency. The early
construction is safe. The first audio tick applies the preference.

### D2 — The row uses the shared stepper

The page has no dropdown. The shared `Stepper` is the established selection
control for a list that does not fit the control column. The row exposes the
ordered list by cycling. It starts at `System default`.

A dropdown or list popup is a separate UI feature. It needs a focus model and
the window concept in [0093](../issues/0093-ui-window-concept.md). This change
does not add one. A device name can be long, so the row must clip the value
without breaking the layout.

### D3 — The engine accepts a missing preferred name

A saved device can disappear between restarts. The service accepts any
non-empty name, keeps it as the preference, reports it as missing on the next
tick, and falls back. A typo in `volume.toml` takes the same path. The service
does not throw and does not return a failure for a missing name.

This matches the acceptance criterion: the service reports a chosen device
that is no longer available and falls back to the default device.

## 4. Engine design

### 4.1 Contract

New file `Swordfish/Audio/IPlaybackDeviceService.cs`:

```csharp
namespace Swordfish.Audio;

/// <summary>
/// Reads and selects the playback device for channels that use the default device.
/// </summary>
public interface IPlaybackDeviceService
{
    /// <summary>Gets the names of the initialized playback devices, in ordinal order.</summary>
    IReadOnlyList<string> PlaybackDeviceNames { get; }

    /// <summary>Gets the name of the system default device, if one is marked.</summary>
    string? DefaultPlaybackDeviceName { get; }

    /// <summary>Gets the saved preference. Null means follow the system default.</summary>
    string? PreferredPlaybackDeviceName { get; }

    /// <summary>Gets the device that default channels use now.</summary>
    string? ActivePlaybackDeviceName { get; }

    /// <summary>Gets whether the preference names a device that is not available.</summary>
    bool IsPreferredPlaybackDeviceMissing { get; }

    /// <summary>Raised after a scan or a preference change alters the device state.</summary>
    event EventHandler<PlaybackDevicesChangedEventArgs>? PlaybackDevicesChanged;

    /// <summary>
    /// Sets or clears the preferred device. Null or empty clears it.
    /// The audio system applies the change on its next tick.
    /// </summary>
    void SetPreferredPlaybackDevice(string? name);
}
```

New file `Swordfish/Audio/PlaybackDevicesChangedEventArgs.cs`:

```csharp
namespace Swordfish.Audio;

/// <summary>Describes the playback device state after a change.</summary>
public readonly struct PlaybackDevicesChangedEventArgs(
    IReadOnlyList<string> playbackDeviceNames,
    string? defaultPlaybackDeviceName,
    string? preferredPlaybackDeviceName,
    string? activePlaybackDeviceName,
    bool isPreferredPlaybackDeviceMissing
)
{
    public IReadOnlyList<string> PlaybackDeviceNames { get; } = playbackDeviceNames;
    public string? DefaultPlaybackDeviceName { get; } = defaultPlaybackDeviceName;
    public string? PreferredPlaybackDeviceName { get; } = preferredPlaybackDeviceName;
    public string? ActivePlaybackDeviceName { get; } = activePlaybackDeviceName;
    public bool IsPreferredPlaybackDeviceMissing { get; } = isPreferredPlaybackDeviceMissing;
}
```

The service is read-only for device state. The setter is the one command.

`AudioSystem` implements the interface. The class stays internal. The container
maps the public interface to the singleton:

```csharp
container.Register<AudioSystem>(Reuse.Singleton);
container.RegisterMapping<IEntitySystem, AudioSystem>();
container.RegisterMapping<IPlaybackDeviceService, AudioSystem>();
```

### 4.2 Resolution

`AudioSystem` keeps these fields:

| Field | Meaning |
|---|---|
| `_preferredPlaybackDeviceName` | the in-memory preference, null for system default |
| `_defaultPlaybackDevice` | the system default device handle |
| `_activePlaybackDevice` | the device that default channels use now |

The scan resolves the active device in this order:

1. If the preference names an initialized device, the active device is that
   device.
2. Else, if the system default is initialized, the active device is the system
   default.
3. Else, the active device is null.

`IsPreferredPlaybackDeviceMissing` is true when the preference is non-empty and
the initialized device map does not contain it.

`ResolveDefaultPlaybackDevice` becomes `ResolveActivePlaybackDevice`. The
routing code at `AudioSystem.cs:399` uses `_activePlaybackDevice` for both the
empty name and the unknown name. A named channel that is unavailable still
falls back to the active device. That keeps the current fallback rule.

A preferred device that returns becomes active on the next scan. No user action
is needed. A preferred device that disappears falls back on the next scan. The
preference value does not change.

The device list comes from the initialized device map, not from the raw
enumeration. A device that fails initialization is not listed. The system
already retries failed devices, so a recovered device appears on a later scan.
Empty names are ignored. The list is sorted with `StringComparer.Ordinal` for a
stable UI order. Duplicate names keep the existing first-name-wins limitation.

### 4.3 Change event and threading

The scan and the preference apply compute the next state. The system raises
`PlaybackDevicesChanged` only when one of these values changes:

- the device name set,
- the system default name,
- the preferred device name,
- the active device name,
- the missing preference flag.

The settings page applies on the render thread. The device map belongs to the
ECS thread. The service must not let the render thread touch the map.

`SetPreferredPlaybackDevice` writes the name to one volatile reference field.
It does not resolve and does not touch the device map. `PreferredPlaybackDeviceName`
returns that value immediately, so the page can keep the applied draft value.

`AudioSystem.Tick` applies the requested preference first, then runs the scan
when the interval is due. It compares the new state with the published state
once. When the state differs, it raises the event. The active device is at most
one tick behind the preference. The preference is visible at once.

The event is raised on the ECS thread inside `AudioSystem.Tick`. The service
does not marshal to the render thread. Each handler runs in a try/catch. A
handler that throws logs a warning and does not stop the audio tick.

Reads return immutable snapshots. The snapshots are replaced as one reference
write. The render thread can read `PlaybackDeviceNames` and the name properties
without a lock.

`AudioSystem.Dispose` clears the event handlers. It does not dispose a preferred
device separately. The existing device disposal path owns every handle.

### 4.4 Engine tests

Extend `Swordfish.Tests/AudioDeviceChangeTests.cs`. The file already has a fake
audio engine and a fake playback device. Add these cases:

| Test | Expected result |
|---|---|
| `Service_ListsInitializedDevices` | sorted names |
| `Service_OmitsFailedDevices` | failed device absent |
| `SetPreferredDevice_SetsActiveName` | active name is the preferred name |
| `SetPreferredDevice_RaisesChanged` | one event with the new active name |
| `ClearPreferredDevice_UsesSystemDefault` | active name is the default name |
| `RemovePreferredDevice_FallsBackAndReportsMissing` | active name is the default, missing flag true, one event |
| `ReturnPreferredDevice_ResumesPreference` | active name returns to the preferred name |
| `UnknownPreferredDevice_IsReportedMissing` | active name is the default, missing flag true |
| `UnchangedScan_DoesNotRaiseChanged` | event count stays zero |

The tests drive `Tick` after each preference change. The scan tests advance by
the existing 2 second interval. They assert the service properties and the event
count. Routing tests assert the active name instead of a live mixer attachment.
The fake audio engine cannot produce a stream, so a live attachment test adds no
coverage.

## 5. Game design

### 5.1 Persistence

Add one key to `VolumeSettings`:

```csharp
public DataBinding<string> OutputDevice { get; private set; } = new(string.Empty);
```

`volume.toml` stores the device name. An empty string means `System default`.
`ConfigManager` applies the value at startup as shown in D1. The service keeps
a copy. The service never writes `volume.toml`.

The apply path writes both values:

```csharp
_volumeSettings.OutputDevice.Set(draft.OutputDevice);
_playbackDeviceService.SetPreferredPlaybackDevice(draft.OutputDevice);
```

`ConfigManager.ApplySettings` then saves `volume.toml` with the other configs.

### 5.2 Draft

`SettingsDraft` adds one field:

```csharp
public string OutputDevice;
```

`FromConfigs` copies `volumeSettings.OutputDevice.Get()`.
`CreateDefault` uses `SettingsDefaults.OUTPUT_DEVICE`. The default is an empty
string, which means `System default`. Reset restores the empty value.

### 5.3 Row

`AudioSettingsCategory.Build` takes `IPlaybackDeviceService` in addition to the
draft and the localization. The audio category gets a new first section:

```text
Output
  Output device    [<]  System default  [>]
Mix
  Master           [slider]
  ...
```

The row uses `StepperEntry<string>` with these values:

| Part | Value |
|---|---|
| id | `Settings.OutputDevice` |
| label key | `ui.settings.option.outputDevice` |
| value | `draft.OutputDevice` |
| default | `string.Empty` |
| options | `string.Empty` first, then the service names, with a missing preference appended |
| format | `System default`, a device name, or `Name (unavailable)` |

The option builder returns the current draft value when the device list no
longer contains it. This mirrors `SettingsDefaults.WithValue`
(`SettingsDefaults.cs:114`). The stepper then shows the missing name instead
of clamping to the first option.

`StepperEntry<T>` changes its options parameter from `IReadOnlyList<T>` to
`Func<IReadOnlyList<T>>`. `Snapshot` and `Step` read the provider once per call.
Every existing stepper call site passes a lambda over its current list. The
display and gameplay categories keep the same options.

### 5.4 Refresh

`SettingsPage` subscribes to `PlaybackDevicesChanged` in `OnEnter` and
unsubscribes in `OnExit`. The handler runs on the ECS thread, so it only sets a
flag with `Interlocked.Exchange`. `RenderPage` consumes the flag on the render
thread:

1. Set `_rowsDirty`.
2. If the output device entry is not dirty, copy the service preference into
   the draft and capture a new baseline. This keeps an external change in sync.
3. If the preferred device is missing, keep the draft value and let the row
   format it as unavailable. Do not change the saved preference.

The stepper reads the options provider on each snapshot, so the row shows the
new list on the next frame. The draft value keeps the selection when the device
is still present. When the device is absent, the appended option keeps the
selection visible.

The scan interval is 2 seconds, so the row updates within 2 seconds of a
hotplug. The page does not poll.

### 5.5 Apply, cancel, and reset

- Apply writes `VolumeSettings.OutputDevice`, calls
  `SetPreferredPlaybackDevice`, and saves `volume.toml`.
- Cancel leaves the config and the service unchanged. The draft is discarded.
- Reset sets the draft to the empty string. Apply then clears the preference
  and returns to the system default.
- The service falls back on the next tick if the selected device is gone. The
  row still shows the preferred name with the unavailable suffix.

### 5.6 Localization

Add these keys to `WaywardBeyond.Client/assets/lang/en/ui.toml`:

| Key | Value |
|---|---|
| `ui.settings.section.output` | `Output` |
| `ui.settings.option.outputDevice` | `Output device` |
| `ui.settings.value.systemDefault` | `System default` |
| `ui.settings.value.unavailable` | `unavailable` |

Follow [ui-copy](../specs/ui-copy.md). The row value is dynamic text, not a new
pattern.

### 5.7 Game tests

- `SettingsEntryTests` covers the dynamic options provider, selection
  retention, and reset to the empty value.
- A new audio category test covers the option order, the `System default`
  entry, the missing-device append, and the unavailable label.
- `SettingsPageRenderTests`, `SettingsShellViewTests`,
  `SettingsInteractionTests`, and `PageHostPaddingTests` add a fake playback
  device service to their `SettingsPage` construction.
- `SettingsInteractionTests` adds one event-driven refresh test: raise the
  fake service event, render one frame, and assert that the new device name
  appears in the row.
- The apply path writes `VolumeSettings.OutputDevice` and calls the service.
  The engine setter tests cover the service side. The page apply has no
  dedicated test because `ConfigManager` is a concrete type and its save path
  needs the full TOML mapper registration.

## 6. Docs

The docs commit records the final behavior:

- `docs/specs/audio.md`: the public service contract, the resolution order, the
  missing preference path, the event thread rule, and the source pointers.
- `docs/specs/settings-ui.md`: the output section, the row, the dynamic options,
  the refresh path, the missing label, and the apply and reset behavior.
- `docs/specs/config-schemas.md`: a new `volume.toml` section with the four
  existing volume keys and `OutputDevice`.
- `docs/issues/0095-output-device-selection.md`: status `done`, every
  acceptance box checked, and the final source pointers.
- `docs/issues/index.md`: check the 0095 entry.
- `docs/proposals/output-device-selection.md`: status `implemented`.

## 7. Commits

Three commits in one branch and one pull request. The order is engine, game,
docs. The engine commit builds and passes its tests alone. The game commit is
the only consumer. The docs commit completes the mandatory docs pass for the
change set.

### Commit 1 — engine: `Add playback device service #748`

| File | Change |
|---|---|
| `Swordfish/Audio/IPlaybackDeviceService.cs` | new public interface |
| `Swordfish/Audio/PlaybackDevicesChangedEventArgs.cs` | new event args |
| `Swordfish/Audio/SoundFlow/AudioSystem.cs` | implement the interface, add the preference, active resolution, snapshots, and event |
| `Swordfish/EngineContainer.cs` | register the service mapping |
| `Swordfish.Tests/AudioDeviceChangeTests.cs` | list, preference, fallback, resume, and event tests |

Gate: `dotnet build`, `dotnet test Swordfish.Tests`.

### Commit 2 — game: `Add output device setting #748`

| File | Change |
|---|---|
| `WaywardBeyond.Client/Configuration/VolumeSettings.cs` | add `OutputDevice` |
| `WaywardBeyond.Client/Configuration/ConfigManager.cs` | apply the saved preference at startup |
| `WaywardBeyond.Client/UI/Settings/SettingsDefaults.cs` | add `OUTPUT_DEVICE` |
| `WaywardBeyond.Client/UI/Settings/SettingsDraft.cs` | add the draft field and copy it |
| `WaywardBeyond.Client/UI/Settings/StepperEntryOfT.cs` | read options from a provider |
| `WaywardBeyond.Client/UI/Settings/DisplaySettingsCategory.cs` | wrap existing option lists in providers |
| `WaywardBeyond.Client/UI/Settings/GameplaySettingsCategory.cs` | wrap existing option lists in providers |
| `WaywardBeyond.Client/UI/Settings/AudioSettingsCategory.cs` | add the output section and device row |
| `WaywardBeyond.Client/UI/Layers/Menus/SettingsPage.cs` | take the service, subscribe, refresh, apply |
| `WaywardBeyond.Client/Injector.cs` | resolve the service into each settings page |
| `WaywardBeyond.Client/assets/lang/en/ui.toml` | add four keys |
| `Swordfish.Tests/ClientFakes.cs` | fake playback device service |
| `Swordfish.Tests/TestLocalization.cs` | output device labels |
| `Swordfish.Tests/SettingsEntryTests.cs` | dynamic options and reset tests |
| `Swordfish.Tests/AudioSettingsCategoryTests.cs` | option order, missing append, and label tests |
| `Swordfish.Tests/SettingsPageRenderTests.cs` | fake service in the fixture |
| `Swordfish.Tests/SettingsShellViewTests.cs` | fake service in the fixture |
| `Swordfish.Tests/SettingsInteractionTests.cs` | fake service, refresh test, apply test |
| `Swordfish.Tests/PageHostPaddingTests.cs` | fake service in the fixture |

Gate: `dotnet build`, `dotnet test Swordfish.Tests`.

### Commit 3 — docs: `Document output device selection #748`

| File | Change |
|---|---|
| `docs/proposals/output-device-selection.md` | mark implemented |
| `docs/specs/audio.md` | service contract and routing |
| `docs/specs/settings-ui.md` | audio row and refresh |
| `docs/specs/config-schemas.md` | `volume.toml` table |
| `docs/issues/0095-output-device-selection.md` | close the issue |
| `docs/issues/index.md` | check the issue entry |

Gate: docs pass, source pointers verified against the final code, no stale
duplicate statements.

## 8. Acceptance criteria mapping

| Criterion | How the plan meets it |
|---|---|
| Read-only service with names, default, and event | `IPlaybackDeviceService` properties and `PlaybackDevicesChanged` |
| Choose or clear the preferred device | `SetPreferredPlaybackDevice(string?)`, empty clears |
| Report a missing device and fall back | `IsPreferredPlaybackDeviceMissing`, event args, active resolution |
| Output device row with `System default` | `Settings.OutputDevice` stepper in a new Output section |
| Persist and apply to default channels | `volume.toml` `OutputDevice`, startup apply, active routing |
| Refresh on device change and keep the selection | service event flag, dynamic options, appended missing value |
| Engine tests for list, resolution, fallback | the eight cases in section 4.4 |
| Audio spec records the contract | `docs/specs/audio.md` in commit 3 |

## 9. Risks and follow-ups

- **Long device names.** The control column is at most 340 pixels. The stepper
  value uses `wrap="false"` and clips to the space between the arrows. Verify
  with a fixture name such as `Speakers (Realtek(R) Audio)`.
- **Stepper versus dropdown.** The row exposes the list by cycling. A player
  with many devices needs several clicks. If this becomes a problem, file a
  follow-up for a list control. Do not add one in this change.
- **ECS-thread event.** Handlers must not touch the UI. The page handler sets a
  flag only. Document this rule in the audio spec.
- **Two-second latency.** A hotplug reaches the row on the next scan. The
  existing scan interval sets this bound. Do not add a second polling path.
- **Duplicate device names.** The engine keeps the first name. The row cannot
  show both. This is an existing limitation. Record it in the audio spec.
- **Server audio config.** The server has no `volume.toml` and no preference.
  The engine service still works with a null preference.
