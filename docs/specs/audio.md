# Audio Playback Devices

One subject: playback device discovery, selection, default routing, and response
to hotplug.

Invariant sound tags and sound-effect requests live in
[sound-effects](sound-effects.md).

## Model

- A playback device is identified by `DeviceInfo.Name`.
- `AudioChannel.PlaybackDevice` selects a device. An empty or null value selects
  the active device.
- The engine initializes every playback device, not only the default. This lets a
  channel target a named device.
- A `SoundPlayer` belongs to the engine and its format. It does not belong to a
  physical device.

## Public service

`IPlaybackDeviceService` is the public read and command surface. `AudioSystem`
implements it, and the engine container maps the interface to the singleton.

`IPlaybackDeviceService.cs:4` defines these members:

| Member | Meaning |
|---|---|
| `PlaybackDeviceNames` | initialized device names, in ordinal order |
| `DefaultPlaybackDeviceName` | the device that the system marks as default |
| `PreferredPlaybackDeviceName` | the caller's preference, null for system default |
| `ActivePlaybackDeviceName` | the device that default channels use now |
| `IsPreferredPlaybackDeviceMissing` | the preference names no available device |
| `PlaybackDevicesChanged` | state-change event |
| `SetPreferredPlaybackDevice` | sets or clears the preference |

The service is read-only for device state. The setter is the one command.

## Selection

The preference is a device name. A null or empty value follows the system
default. The system resolves the active device in this order:

1. The preferred device, when it is initialized.
2. The system default device, when it is initialized.
3. No device.

A preferred device that is absent sets `IsPreferredPlaybackDeviceMissing` to
true and falls back to the system default. The preference value does not change.
A device that returns becomes active on the next scan. The player does not need
to select it again.

A channel with an empty `PlaybackDevice` uses the active device. A channel that
names an unavailable device also falls back to the active device. The engine
does not route a named channel to another named device.

The engine holds the preference in memory for one process. It does not persist
the preference. The host owns persistence. Wayward Beyond stores the value in
`volume.toml` and applies it at startup. See [settings-ui](settings-ui.md).

## Discovery

- The audio system enumerates playback devices at startup.
- The audio system enumerates again every 2 seconds. The scan runs on the ECS
  thread.
- `PlaybackDeviceScanInterval` sets the interval.
- SoundFlow 1.3.0 has no device-change event. `AudioEngine.UpdateAudioDevicesInfo()`
  re-enumerates through miniaudio on each call.
- A failed scan keeps the previous device set and logs a warning.
- The device list contains initialized devices only. A device that fails
  initialization is absent, and the system retries it on a later scan.
- Empty device names are ignored. The list is sorted with `StringComparer.Ordinal`.

## Change handling

- A new device is initialized with the configured audio format and added to the
  device map.
- A removed device is detached from its sound players, disposed, and removed
  from the device map.
- A default change updates the system default name.
- A preference change updates the active device. Channels that use the active
  device move to the new device on the next tick.
- When no device is marked default and the preference is absent or missing, the
  active reference is null. Default channels stay silent until a device appears.
- Sound players are re-parented across devices. The engine does not recreate
  them.

## Change notification

- `SetPreferredPlaybackDevice` writes the requested name and does not touch the
  device map. The render thread can call it.
- `AudioSystem.Tick` applies the requested preference before the scan. The
  active device is at most one tick behind the preference.
- `AudioSystem.Tick` raises `PlaybackDevicesChanged` when the device names, the
  system default, the preference, the active device, or the missing flag change.
- The event runs on the ECS thread. A handler must not touch the UI directly. It
  sets a flag that the render thread consumes.
- A handler that throws logs a warning. The audio tick continues.
- The service publishes immutable snapshots. The render thread can read the
  properties without a lock.

## Logging

- The log records each transition between a default device and no default
  device.
- The log records each device add and each device remove.
- A device initialization failure logs a warning on the first failure and a
  debug message on later retries.
- A failed change handler logs a warning.

## Limitations

- Duplicate device names collapse into one map entry. The first name wins.
- A device that disconnects and reconnects between two scans keeps its previous
  handle.
- The scan cost is accepted at the 2 second interval. Move enumeration off the
  ECS thread if the scan causes frame hitches.

## Source of truth

- `Swordfish/Audio/IPlaybackDeviceService.cs:4` — public service contract.
- `Swordfish/Audio/PlaybackDevicesChangedEventArgs.cs:4` — event payload.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:14` — service implementation.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:42` — published properties.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:57` — change event.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:77` — preference setter.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:90` — tick and preference apply.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:114` — scan.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:146` — active resolution and publish.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:232` — state comparison.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:276` — handler dispatch.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:319` — remove device.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:399` — route a player.
- `Swordfish/Audio/AudioChannel.cs:5` — channel device selection.
- `Swordfish/EngineContainer.cs:108` — service registration.
- `Swordfish/Settings/PlaybackSettings.cs:5` — format settings.
- `WaywardBeyond.Client/Configuration/VolumeSettings.cs:14` — persisted preference.
- `WaywardBeyond.Client/Configuration/ConfigManager.cs:53` — startup apply.
- `WaywardBeyond.Client/UI/Layers/Menus/SettingsPage.cs:303` — apply from the page.

## Tests

- `Swordfish.Tests/AudioDeviceChangeTests.cs` covers device discovery, removal,
  default detection, the device list, preference resolution, fallback, return,
  and event suppression.
- `WaywardBeyond.Tests/AudioSettingsCategoryTests.cs` covers the output device row
  options, the missing-device label, and reset.
- `WaywardBeyond.Tests/SettingsInteractionTests.cs` covers the refresh after a
  device change.
