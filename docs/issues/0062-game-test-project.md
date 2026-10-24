# Improvement: Move game tests out of the engine test project

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`Swordfish.Tests` references `WaywardBeyond.Client`, `WaywardBeyond.Server`, and
the other game modules, and holds the game test files: `StageRingTests`,
`SimulationStepIdleTests`, `SimulationStepLookHistoryTests`,
`InventoryResolverTests`, `InteractionResolverTests`, `InteractionHandlerRegistryTests`,
`NetworkComponentTests`, and the older networking and integration suites.

The commit rules split engine and game work. A game test in the engine test
project makes a game change an engine-project change, so the two cannot be
committed as separate sets that each build standalone. The branch already deleted
both former game test projects.

## Acceptance criteria

- [ ] A game test project exists and holds the game tests.
- [ ] `Swordfish.Tests` no longer references game projects.
- [ ] The solution and the CI workflow include the new project.
- [ ] `dotnet test` for the engine project passes without the game modules built.
