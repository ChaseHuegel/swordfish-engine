# Structure Power Grid

One subject: how the server simulates one power bus per voxel structure, sums
generation and demand, burns generator fuel, sheds consumers during a brownout,
and publishes telemetry.

## Model

Power is structure-level. There is no wire block and no per-island splitting.
Every voxel structure entity (`VoxelEntity`) hosts at most one bus.

| Type | Where | Role |
|---|---|---|
| `PowerNodeComponent` | brick-entity | signed rating, standby draw, brownout tier, fuel capability |
| `PowerGridComponent` | structure entity | last published supply, demand, status, and node counts |
| `GridPowerTelemetry` | value struct | immutable snapshot for pressurization, machine simulation, and WAY-OS |
| `ServerPowerGridSystem` | server world | evaluates buses, burns fuel, allocates power |
| `PowerBrickEntityDecorator` | server decorator | attaches `PowerNodeComponent` from brick statistics |

`PowerBrickEntityDecorator` (`WaywardBeyond.Server/Systems/PowerBrickEntityDecorator.cs:16`)
applies to any brick whose `wb:power_kw` statistic is finite and non-zero. It
copies the rating, the optional standby draw, the resolved station tier, and
whether the brick declares the `wb:fuel` slot group onto
`PowerNodeComponent` (`WaywardBeyond.Gameplay/Components/PowerNodeComponent.cs:9`).
`ServerBrickEntityService` installs the decorator by default and
`ServerComposition` registers it for the DI graph
(`WaywardBeyond.Server/ServerComposition.cs:87`).

`ServerPowerGridSystem`
(`WaywardBeyond.Server/Systems/ServerPowerGridSystem.cs:23`) is a server world
system. It ticks after chat and before the brick-entity viewer and publish
stages (`WaywardBeyond.Server/ServerComposition.cs:137`).

## Supply and demand

`wb:power_kw` is signed. A positive rating is generation. A negative rating is
demand.

| State | Supply | Demand |
|---|---|---|
| producer enabled | `max(RatedKw, burning fuel output)` | none |
| producer disabled | none | none |
| consumer enabled and processing | none | `abs(RatedKw)` |
| consumer enabled and idle | none | `wb:power_standby_kw` when positive, otherwise `abs(RatedKw)` |
| consumer disabled | none | none |

A consumer counts as processing when
`MachineStateComponent.ActiveRecipeId` is not empty. Standby lets content declare
an idle draw lower than the active draw. When the statistic is absent or zero,
the machine draws its full rating whenever it is enabled.

Fuel burning applies to an enabled producer that declares a `wb:fuel` slot
group. On each evaluation the system looks for an automatic recipe with target
tag `station.generator` that the current fuel stacks satisfy
(`RecipeDatabase.FindAutoMatch`). A valid recipe has `PowerKw > 0` and
`DurationSeconds > 0`. Ignition consumes the ingredient stack immediately. The
fueled output replaces the baseline only while it is higher, so a 50 kW core
with a 60 kW biomass recipe supplies 60 kW. When the burn ends, the next
satisfying unit ignites at once. Burn time advances every server tick, so the
timer stays accurate even between structure scans. Burn state is volatile and is
not persisted.

## Brownout priority

Enabled consumers sort by tier and then by stable voxel coordinate order.

| Tier | Station tags |
|---|---|
| `LIFE_SUPPORT` (`0`) | `station.console`, `station.o2_extractor` |
| `PROCESSOR` (`1`) | `station.refinery`, `station.synthesizer`, `station.composter`, `station.hydroponic_bay`, `station.oven` |
| `AUXILIARY` (`2`) | every other station tag, including generators |

`PowerGridPriority.Resolve` (`WaywardBeyond.Gameplay/Power/PowerGridPriority.cs:39`)
maps the tag. Allocation is greedy by tier in ascending order: a tier keeps
power only when the whole tier fits in the remaining supply. Once a tier does not
fit, that tier and every lower tier are shed. This keeps life support alive
before any processor. Within one tier, coordinate order keeps the result
deterministic.

The shed consumer receives `MachineStateComponent.HasPower = false` and the
`NoPower` status flag. A manually disabled node receives `HasPower = false` and
no `NoPower` flag, because it lacks no power. Enabled generators receive
`HasPower = true`.

## Status

| Status | Meaning |
|---|---|
| `Nominal` | enabled demand fits enabled supply |
| `Brownout` | demand exceeds supply and the highest tier keeps power |
| `Overload` | demand exceeds supply and even the highest tier cannot be powered |

`GridPowerTelemetry.Brownout` is true for `Brownout` and `Overload`
(`WaywardBeyond.Gameplay/Power/GridPowerTelemetry.cs:42`).

## Telemetry

`ServerPowerGridSystem.PublishStructure` writes `PowerGridComponent` on the
structure entity whenever a field changes. The component exposes
`Telemetry`, an immutable `GridPowerTelemetry` with `SupplyKw`, `DemandKw`,
`NetKw`, `LoadPercent`, `Brownout`, `ProducerCount`, and `ConsumerCount`.
`LoadPercent` reports `100` when demand exists with no supply and `0` when both
are zero. Consumers call
`ServerPowerGridSystem.GetTelemetry(store, structureUuid)`
(`WaywardBeyond.Server/Systems/ServerPowerGridSystem.cs:70`).

The component is server-only. Issue #941 owns the client wire path for the
WAY-OS telemetry view.

## Evaluation cadence

A full scan runs at most once per
`EVALUATION_INTERVAL_SECONDS` (`0.5` seconds). The scan walks
`PowerNodeComponent` entities once, groups them by structure, and updates only
changed components. Idle structures therefore cost one bounded scan per
interval and no per-tick writes.

Call `ServerPowerGridSystem.MarkDirty(structureUuid)` after an external code path
changes a machine state that the next scan must see at once, such as a power
toggle or an active recipe change. Burn completion already marks its own
structure. Brick placement and removal appear in the next scan without a call.

When a structure loses every power node, the system removes its
`PowerGridComponent` and drops its burn records.

## Source of truth

- `WaywardBeyond.Gameplay/Power/GridPowerTelemetry.cs`
- `WaywardBeyond.Gameplay/Power/PowerGridStatus.cs`
- `WaywardBeyond.Gameplay/Power/PowerGridPriority.cs`
- `WaywardBeyond.Gameplay/Components/PowerNodeComponent.cs`
- `WaywardBeyond.Gameplay/Components/PowerGridComponent.cs`
- `WaywardBeyond.Server/Systems/PowerBrickEntityDecorator.cs`
- `WaywardBeyond.Server/Systems/ServerPowerGridSystem.cs`
- `WaywardBeyond.Data/Statistics/StatisticIds.cs:25-28`

## Tests that pin this

`WaywardBeyond.Tests/PowerGridTests.cs` pins nominal balance and telemetry,
standby versus processing demand, generator fuel ignition and depletion,
deterministic brownout shedding, overload, power toggles, decorator gating, and
node removal cleanup.
