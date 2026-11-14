# Improvement: Allow output device selection

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

The engine detects playback device changes and keeps the device map current. It
does not expose a public way to list the playback devices or to choose one.

`AudioSystem` owns the device map and is internal. `AudioChannel.PlaybackDevice`
accepts a device name, but a caller has no supported way to discover the names,
read the current default, or react to a device change.

Wayward Beyond has no output device control in the audio settings page. A player
can use the system default only. The game cannot show the available devices,
persist a preference, or tell the player when the chosen device disappears.

## Acceptance criteria

- [x] The engine exposes a read-only playback device service with the device names, the current default name, and a device-changed event.
- [x] The service exposes a supported way to choose the preferred playback device by name, or to clear the preference and follow the system default.
- [x] The service reports a chosen device that is no longer available and falls back to the default device.
- [x] Wayward Beyond adds an output device row to the audio settings category. The row lists the available devices and includes a "System default" choice.
- [x] The selected device persists across restarts and applies to channels that use the default device.
- [x] The settings row refreshes when the device list changes and keeps the current selection when possible.
- [x] Engine tests cover the device list, the preferred device resolution, and the fallback path.
- [x] The audio spec records the public device service contract and the settings behavior.

## Implementation

`IPlaybackDeviceService` is the public contract. `AudioSystem` implements it, and
the engine container maps the interface to the singleton. The service resolves
the preferred device first, then the system default. A missing preference falls
back and reports the missing state. See [audio](../specs/audio.md).

The game stores the preference in `volume.toml` and applies it at startup. The
audio settings category has an `Output` section with a `Settings.OutputDevice`
stepper. The row starts with `System default`, appends a missing selection, and
refreshes from the service event. See [settings-ui](../specs/settings-ui.md).

The design record is
[output-device-selection](../proposals/output-device-selection.md).
