# Spatial Audio

One subject: 3D audio listener resolution, directional stereo panning, distance
attenuation, and Doppler pitch shift.

Playback device discovery and routing live in [audio](audio.md). Game sound
tags and brick triggers live in [sound-effects](sound-effects.md).

## Model

- SoundFlow 1.4.2 does not provide built-in 3D spatialization. Swordfish
  computes 3D attenuation, stereo pan, and Doppler pitch shift in
  `SpatialAudioSolver` and applies the results to each `SoundPlayer`.
- An entity with `AudioListener` and `TransformComponent` is the active 3D
  listener. `AudioSystem` selects the first matching entity on each tick.
- `ECSContext` attaches `AudioListener` to the default main camera at startup.
- An audio entity with `AudioPlayer`, `AudioSource`, `TransformComponent`, and
  `SpatialAudio` plays as a 3D spatial source.
- An audio entity without `SpatialAudio` or without `TransformComponent` plays
  as a centered 2D source (`Pan = 0.5`, unattenuated volume, unmodified pitch).
- When no active listener exists in the world, spatial sources fall back to
  centered 2D playback.

## Coordinate frame and stereo panning

- Swordfish uses a right-handed coordinate frame where `-Z` is forward, `+Y` is
  up, and `+X` (`TransformComponent.GetRight()`) is right.
- `SpatialAudioSolver.ComputePan` projects the unit vector from the listener to
  the source onto the listener right axis and maps `[-1, 1]` to `[0, 1]`.
- `0.0` is full left, `0.5` is center, and `1.0` is full right. SoundFlow
  applies constant-power stereo panning on `SoundPlayer.Pan`.
- Sources directly ahead, behind, above, below, or coinciding with the listener
  pan to `0.5`.

## Distance attenuation

`SpatialAudio` configures attenuation with `MinDistance` (default `4`),
`MaxDistance` (default `64`), `RolloffFactor` (default `1`), and `RolloffMode`
(default `AudioRolloffMode.Inverse`):

- Within `MinDistance`, gain is `1.0`.
- At or beyond `MaxDistance` (when `MaxDistance > MinDistance`), gain is `0.0`.
- When `RolloffFactor <= 0`, gain stays `1.0` inside `MaxDistance`.
- `AudioRolloffMode.Inverse` (default) uses
  `min / (min + rolloff * (d - min))`, normalized to reach `0.0` continuously
  at `MaxDistance`. Setting `MaxDistance <= MinDistance` leaves the curve
  unbounded.
- `AudioRolloffMode.Linear` falls off linearly from `1.0` at `MinDistance` to
  `0.0` at `MaxDistance`.
- `AudioRolloffMode.Exponential` uses `(d / min)^(-rolloff)`, normalized to
  reach `0.0` continuously at `MaxDistance`. Setting `MaxDistance <= MinDistance`
  leaves the curve unbounded.

## Doppler pitch shift

- `AudioSystem.ResolveVelocity` determines velocity for the listener and for
  each spatial source in this order:
  1. `PhysicsComponent.Velocity` on the entity.
  2. `PhysicsComponent.Velocity` on the entity's `ChildComponent.Parent` (for
     example, a first-person camera childed to a physics-driven player).
  3. Finite-difference position change `(position - previousPosition) / delta`
     when the entity has a prior tick position and `delta > 0`.
  4. `Vector3.Zero` on the first tick or when `delta <= 0`.
- `SpatialAudioSolver.ComputeDopplerPitch` computes
  `(c + vListener) / (c + vSource)` using radial velocity components projected
  onto the listener-to-source direction and scaled by
  `SpatialAudio.DopplerFactor * SpatialAudioSettings.DopplerScale`.
- Radial velocity deltas clamp to `±0.95 * SpeedOfSound` before division, and
  the final pitch multiplier clamps to `[MinDopplerPitch, MaxDopplerPitch]`.
- Setting `SpatialAudio.DopplerFactor` or `SpatialAudioSettings.DopplerScale`
  to `0` disables Doppler pitch shift.

## Spatial blend

- `SpatialAudio.SpatialBlend` clamps to `[0, 1]` and interpolates volume
  attenuation, stereo pan, and Doppler pitch shift between 2D (`0`) and full
  3D (`1`).

## Configuration

`audio.toml` exposes the `[Spatial]` table through `AudioSettings.Spatial`. See
[config-schemas](config-schemas.md).

## Source of truth

- `Swordfish/Audio/AudioListener.cs:7` — listener marker component.
- `Swordfish/Audio/AudioRolloffMode.cs:4` — rolloff curve enum.
- `Swordfish/Audio/SpatialAudio.cs:7` — spatial source component.
- `Swordfish/Audio/SpatialAudioSolver.cs:6` — attenuation, panning, and Doppler solver.
- `Swordfish/Audio/SpatialAudioSolver.cs:15` — combined solve entry point.
- `Swordfish/Audio/SpatialAudioSolver.cs:56` — distance attenuation.
- `Swordfish/Audio/SpatialAudioSolver.cs:119` — directional stereo panning.
- `Swordfish/Audio/SpatialAudioSolver.cs:126` — Doppler pitch shift.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:131` — listener resolution.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:167` — velocity resolution.
- `Swordfish/Audio/SoundFlow/AudioSystem.cs:567` — per-player spatial evaluation.
- `Swordfish/ECS/ECSContext.cs:47` — default main camera listener attachment.
- `Swordfish/Settings/AudioSettings.cs:5` — audio config root.
- `Swordfish/Settings/SpatialAudioSettings.cs:6` — spatial audio config keys.

## Tests

- `Swordfish.Tests/SpatialAudioTests.cs` covers attenuation curves, stereo
  panning across listener rotations, Doppler pitch shift and clamping, spatial
  blend, velocity resolution (physics, child parent physics, finite difference),
  missing-listener fallback, and runtime settings updates.
