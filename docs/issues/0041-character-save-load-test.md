# Bug: CharacterSaveManagerTests session-time test never sets the active save

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`CharacterSaveManagerTests.SaveRightAfterSessionBegin_CountsOnlySessionTime_NotIdleGap`
(`WaywardBeyond.Client.Core.Tests/CharacterSaveManagerTests.cs:37`) calls
`saves.Load()` and asserts success. `CharacterSaveManager.Load` returns a
failure when `ActiveSave` is null (`CharacterSaveManager.cs:25`). The test
passes a fresh `ActiveCharacterSave` and a stub `ICharacterStorage`, but never
assigns `saves.ActiveSave`. `ActiveCharacterSave` has no storage link
(`ActiveCharacterSave.cs`). The test therefore fails at
`CharacterSaveManagerTests.cs:64` on every run.

The failure is independent of the multiworld and NATS work. It was found while
running the game test suite.

## Acceptance criteria

- [ ] The test sets the active save before `Load`, or `Load` is corrected to
      seed it from storage, matching the intended contract.
- [ ] `dotnet test WaywardBeyond.Client.Core.Tests` passes.
