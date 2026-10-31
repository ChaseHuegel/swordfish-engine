# Audio Playback Devices

One subject: playback device discovery, default routing, and response to hotplug.

## Model

- A playback device is identified by `DeviceInfo.Name`.
- `AudioChannel.PlaybackDevice` selects a device. An empty or null value selects the current system default.
- The engine initializes every playback device, not only the default. This lets a channel target a named device.
- A `SoundPlayer` belongs to the engine and its format. It does not belong to a physical device.

## Discovery

- The audio system enumerates playback devices at startup.
- The audio system enumerates again every 2 seconds. The scan runs on the ECS thread.
- `PlaybackDeviceScanInterval` sets the interval.
- SoundFlow 1.3.0 has no device-change event. `AudioEngine.UpdateAudioDevicesInfo()` re-enumerates through miniaudio on each call.
- A failed scan keeps the previous device set and logs a warning.

## Change handling

- A new device is initialized with the configured audio format and added to the device map.
- A removed device is detached from its sound players, disposed, and removed from the device map.
- A default change updates the default device reference. Channels that use the default move to the new device on the next tick.
- When no device is marked default, the default reference is null. Default channels stay silent until a default appears.
- A channel that names an unavailable device falls back to the default device.
- Sound players are re-parented across devices. The engine does not recreate them.

## Logging

- The log records each transition between a default device and no default device.
- The log records each device add and each device remove.
- A device initialization failure logs a warning on the first failure and a debug message on later retries.

## Limitations

- Duplicate device names collapse into one map entry. The first name wins.
- A device that disconnects and reconnects between two scans keeps its previous handle.
- The scan cost is accepted at the 2 second interval. Move enumeration off the ECS thread if the scan causes frame hitches.

## Source of truth

- `Swordfish/Audio/SoundFlow/AudioSystem.cs:16` — scan interval.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:54` — tick-driven scan.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:70` — scan and apply.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:118` — add device.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:140` — remove device.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:156` — detach sound players.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:167` — resolve default.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:228` — route a player.
- `Swordfish/Audio/SoundFlow/PlaybackDeviceSet.cs:9` — device set comparison.
- `Swordfish/Audio/AudioChannel.cs:5` — channel device selection.
- `Swordfish.Tests/AudioDeviceChangeTests.cs:19` — device change tests.
