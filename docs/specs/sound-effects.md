# Sound Effects

One subject: invariant sound tags, brick sound declarations, and the client
playback path.

## Sound tags (`lang/**/tags/*.toml`)

Sound tags reuse the invariant tag lists that `LocalizedTagsDatabase` owns. A
sound tag maps to audio clip ids relative to the `audio/` asset root. The file
uses the shared tag format with an empty `Language`. The shipped list is
`WaywardBeyond.Client/assets/lang/tags/sounds.toml`.

```toml
"Language" = ""

[Tags]
"metal_place" = [
    "sounds/place/metal/metal_place.1.wav",
]
```

The client reads only the invariant list, because clip ids are not localized. A
tag with no values is a silent no-op. A clip id with no matching audio stream is
silent at playback. See [asset-definitions](asset-definitions.md).

## Requests

`SoundEffectService.PlayEffect(brick, eventKind, worldPosition)` reads the
brick's declared tags and plays a random clip on the `effects` channel
(`WaywardBeyond.Client/Services/SoundEffectService.cs:31`). When
`worldPosition` is provided, the queued entity also receives
`TransformComponent` and `SpatialAudio` for 3D playback relative to the main
camera's `AudioListener`. See [spatial-audio](spatial-audio.md). When
`worldPosition` is omitted, the effect plays as a centered 2D sound.

`SoundEffectService.PlayInterface(tag)` plays a random clip on the `interface`
channel as a 2D sound (`SoundEffectService.cs:42`). A request names tags, never
clip ids.

A request that carries several tags uses every value from every tag as a
candidate. Each candidate clip is equally likely.

## Brick triggers

`BrickEvent` carries a brick's voxel id, a `BrickEventKind`, and an optional
`WorldPosition` (`WaywardBeyond.Client/Events/BrickEvent.cs:7`). Two paths
raise it:

- `PlayerInteractionService.FirePresentationHook` raises it after `PlaceEvent`
  or `BreakEvent` succeeds (`WaywardBeyond.Client/Systems/PlayerInteractionService.cs:718`).
- `ClientVoxelReconcileSystem.RaiseEditEvent` raises it for unpredicted
  authoritative edits (`WaywardBeyond.Client/Systems/ClientVoxelReconcileSystem.cs:166`).

`BrickSoundListener` handles `BrickEvent`, returns early when `Kind` is not
`Place` or `Break`, resolves the `Brick` from `IBrickDatabase`, and calls
`SoundEffectService.PlayEffect` with `e.WorldPosition`
(`WaywardBeyond.Client/Services/BrickSoundListener.cs:9`). Neither producer
calls `SoundEffectService` for brick audio. Raising `BrickEvent` does not
invoke `PlaceEvent` or `BreakEvent`, so remote edits do not change local
character statistics.

## Brick declarations

Brick tomls map each `BrickEventKind` to sound tags in a `[Bricks.Sounds]`
table:

```toml
[Bricks.Sounds]
Place = ["metal_place"]
Break = ["metal_break"]
```

`Brick.Sounds` carries the map. A brick without a table, or without the event
key, is silent for that event. The base bricks use `metal_place` and
`metal_break`. The environment bricks `wb:rock` and `wb:ice` use `rock_place`
and `rock_break`. See [asset-definitions](asset-definitions.md).

## Interface sounds

`WidgetInteraction.GetSoundTag` maps `InterfaceSound` to the `ui_click`,
`ui_exit`, `ui_tap_increase`, and `ui_tap_decrease` tags
(`WaywardBeyond.Client/UI/WidgetInteraction.cs:16`). The service plays them on
the `interface` channel. See [ui-theme](ui-theme.md).

## Assets

- `WaywardBeyond.Client/assets/lang/tags/sounds.toml` holds the invariant sound
  tags.
- Clips live under `WaywardBeyond.Client/assets/audio/sounds/`.

## Source of truth

- `WaywardBeyond.Client/Events/BrickEvent.cs:7` — brick presentation event.
- `WaywardBeyond.Client/Services/BrickSoundListener.cs:9` — brick sound event listener.
- `WaywardBeyond.Client/Systems/PlayerInteractionService.cs:718` — local prediction trigger.
- `WaywardBeyond.Client/Systems/ClientVoxelReconcileSystem.cs:166` — unpredicted authoritative edit trigger.
- `WaywardBeyond.Client/Systems/AudioChannelSystem.cs:59` — 2D and spatial play queueing.
- `WaywardBeyond.Client/Services/SoundEffectService.cs:14` — playback service.
- `WaywardBeyond.Client/Services/SoundEffectService.cs:31` — brick effect request.
- `WaywardBeyond.Client/Services/SoundEffectService.cs:42` — interface request.
- `WaywardBeyond.Client/Services/SoundEffectService.cs:47` — shared tag lookup.
- `WaywardBeyond.Client/UI/WidgetInteraction.cs:16` — interface tag mapping.
- `WaywardBeyond.Bricks/BrickEventKind.cs:4` — brick event kind enum.
- `WaywardBeyond.Bricks/BrickDefinition.cs:16` — brick sound declaration.
- `WaywardBeyond.Bricks/Brick.cs:25` — loaded brick sound map.
- `WaywardBeyond.Bricks/BrickDatabase.cs:91` — brick sound wiring.
- `WaywardBeyond.Data/Localization/ILocalizedTagsDatabase.cs:6` — tag contract.
- `WaywardBeyond.Data/Localization/LocalizedTagsDatabase.cs:13` — tag loader.
- `WaywardBeyond.Data/Localization/LocalizedTags.cs:17` — tag value lookup.
- `WaywardBeyond.Data/Localization/LocalizedTagValues.cs:6` — ordered tag value set.

## Tests

- `WaywardBeyond.Tests/BrickSoundListenerTests.cs` covers local prediction
  triggers, unpredicted authoritative triggers, prediction echo silence,
  statistic isolation, spatial brick audio attachment, and 2D interface and
  positionless fallbacks.
- `WaywardBeyond.Tests/SoundTagTests.cs` covers the shipped brick and interface
  sound tags, their clip resolution, and the presence of every clip file.
- `WaywardBeyond.Tests/BrickDatabaseTests.cs` covers specific shipped brick
  declarations.
