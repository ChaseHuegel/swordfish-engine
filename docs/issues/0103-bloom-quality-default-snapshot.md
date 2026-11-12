# Bug: A fresh render state reports Low bloom while the declared default is Medium

- Type: bug
- Status: done
- Workflow: ../specs/issues.md

## Problem

The game declares `SettingsDefaults.BLOOM_QUALITY = BloomQuality.Medium`
(`WaywardBeyond.Client/UI/Settings/SettingsDefaults.cs:19`). A fresh
`RenderSettings` starts bloom at passes `2` and intensity `1f`
(`Swordfish/Settings/BloomSettings.cs:8-9`). `BloomQualityExtensions.FromSettings`
maps that pair to `Low`
(`WaywardBeyond.Client/UI/Settings/BloomQualityExtensions.cs:33-60`).

`SettingsDraft.FromConfigs` reads the live render settings
(`WaywardBeyond.Client/UI/Settings/SettingsDraft.cs:65`). The first settings
page therefore shows Low. The declared default and the actual state disagree.

`SettingsShellViewTests.SettingsShellMatchesSnapshots` constructs a bare
`RenderSettings`. The test fails on every run:

- the golden `settings-shell-wide.txt` expects `ui.settings.value.medium`;
- the actual output is `ui.settings.value.low`.

The failure is deterministic. It also fails when run alone, so it is not the
transport flake in 0063.

## Acceptance criteria

- [x] The client applies `SettingsDefaults.BLOOM_QUALITY` to the render settings
      at composition, before the settings page reads them.
- [x] A fresh client reports `FromSettings(Bloom) == SettingsDefaults.BLOOM_QUALITY`.
- [x] The snapshot fixture uses the same composition path, so
      `SettingsShellMatchesSnapshots` passes without regenerating the golden to
      Low.
- [x] A unit test pins the round trip for Low, Medium, High, and Custom.
- [x] No engine default changes. The fix stays in the client, per the
      engine/game split.
