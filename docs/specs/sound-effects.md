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

`SoundEffectService.PlayEffect(brick, soundEvent)` reads the brick's declared
tags and plays a random clip on the `effects` channel
(`WaywardBeyond.Client/Services/SoundEffectService.cs:29`).
`SoundEffectService.PlayInterface(tag)` plays a random clip on the `interface`
channel (`SoundEffectService.cs:40`). A request names tags, never clip ids.

A request that carries several tags uses every value from every tag as a
candidate. Each candidate clip is equally likely.

## Brick triggers

`BrickEditEvent` carries a brick's voxel id and a `BrickSoundEvent`
(`WaywardBeyond.Client/Events/BrickEditEvent.cs:6`). Two paths raise it:

- `PlayerInteractionService.FirePresentationHook` raises it after `PlaceEvent`
  or `BreakEvent` succeeds (`WaywardBeyond.Client/Systems/PlayerInteractionService.cs:652`).
- `ClientVoxelReconcileSystem.RaiseEditEvent` raises it for unpredicted
  authoritative edits (`WaywardBeyond.Client/Systems/ClientVoxelReconcileSystem.cs:157`).

`BrickSoundListener` handles `BrickEditEvent`, resolves the `Brick` from
`IBrickDatabase`, and calls `SoundEffectService.PlayEffect`
(`WaywardBeyond.Client/Services/BrickSoundListener.cs:9`). Neither producer
calls `SoundEffectService` for brick audio. Raising `BrickEditEvent` does not
invoke `PlaceEvent` or `BreakEvent`, so remote edits do not change local
character statistics.

## Brick declarations

Brick tomls map each `BrickSoundEvent` to sound tags in a `[Bricks.Sounds]`
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

- `WaywardBeyond.Client/Events/BrickEditEvent.cs:6` — brick presentation event.
- `WaywardBeyond.Client/Services/BrickSoundListener.cs:9` — brick sound event listener.
- `WaywardBeyond.Client/Systems/PlayerInteractionService.cs:652` — local prediction trigger.
- `WaywardBeyond.Client/Systems/ClientVoxelReconcileSystem.cs:157` — unpredicted authoritative edit trigger.
- `WaywardBeyond.Client/Services/SoundEffectService.cs:12` — playback service.
- `WaywardBeyond.Client/Services/SoundEffectService.cs:29` — brick effect request.
- `WaywardBeyond.Client/Services/SoundEffectService.cs:40` — interface request.
- `WaywardBeyond.Client/Services/SoundEffectService.cs:45` — shared tag lookup.
- `WaywardBeyond.Client/UI/WidgetInteraction.cs:16` — interface tag mapping.
- `WaywardBeyond.Bricks/BrickSoundEvent.cs:4` — brick event enum.
- `WaywardBeyond.Bricks/BrickDefinition.cs:16` — brick sound declaration.
- `WaywardBeyond.Bricks/Brick.cs:25` — loaded brick sound map.
- `WaywardBeyond.Bricks/BrickDatabase.cs:91` — brick sound wiring.
- `WaywardBeyond.Data/Localization/ILocalizedTagsDatabase.cs:6` — tag contract.
- `WaywardBeyond.Data/Localization/LocalizedTagsDatabase.cs:13` — tag loader.
- `WaywardBeyond.Data/Localization/LocalizedTags.cs:11` — tag lookup.

## Tests

- `WaywardBeyond.Tests/BrickSoundListenerTests.cs` covers local prediction
  triggers, unpredicted authoritative triggers, prediction echo silence, and
  statistic isolation.
- `WaywardBeyond.Tests/SoundTagTests.cs` covers the shipped brick and interface
  sound tags, their clip resolution, and the presence of every clip file.
- `WaywardBeyond.Tests/BrickDatabaseTests.cs` covers specific shipped brick
  declarations.
