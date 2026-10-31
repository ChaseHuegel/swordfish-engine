# Improvement: Allow output device selection

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

The engine now detects playback device changes and keeps the device map current.
It does not expose a public way to list the playback devices or to choose one.

`AudioSystem` owns the device map and is internal. `AudioChannel.PlaybackDevice`
accepts a device name, but a caller has no supported way to discover the names,
read the current default, or react to a device change.

Wayward Beyond has no output device control in the audio settings page. A player
can select the system default only. The game cannot show the available devices,
cannot persist a preference, and cannot tell the player when the chosen device
disappears.

## Acceptance criteria

- [ ] The engine exposes a read-only playback device service with the device names, the current default name, and a device-changed event.
- [ ] The service exposes a supported way to choose the preferred playback device by name, or to clear the preference and follow the system default.
- [ ] The service reports a chosen device that is no longer available and falls back to the default device.
- [ ] Wayward Beyond adds an output device row to the audio settings category. The row lists the available devices and includes a "System default" choice.
- [ ] The selected device persists across restarts and applies to channels that use the default device.
- [ ] The settings row refreshes when the device list changes and keeps the current selection when possible.
- [ ] Engine tests cover the device list, the preferred device resolution, and the fallback path.
- [ ] The audio spec records the public device service contract and the settings behavior.
