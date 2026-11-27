# Engine Physics

One subject: Jolt simulation stepping, contact event capture, and thread safety in `Swordfish.Physics`.

## Simulation and contact pipeline

`IPhysics` (`Swordfish/Physics/IPhysics.cs:5-28`) exposes fixed-step simulation events, raycasts, shape overlap queries, and contact notifications. Default interface members allow minimal test stubs to omit `ContactResolved` (`IPhysics.cs:13-17`).

`JoltPhysicsSystem` (`Swordfish/Physics/Jolt/JoltPhysicsSystem.cs:18-566`) owns the Jolt `PhysicsSystem` and registers `OnContactAdded` during construction (`JoltPhysicsSystem.cs:109-110`). It focuses on `PhysicsContactKind.Added` (`Swordfish/Physics/PhysicsContactKind.cs:4-11`) so resting bodies do not emit repeated per-tick events.

Each fixed step runs `StepSolver` (`JoltPhysicsSystem.cs:227-246`):

1. Inside `_solverLock`, `FixedUpdate` fires, queued thread-context releases drain, `PhysicsComponentProcessor` destroys removed bodies, `SyncJoltToEntityAction` pushes entity changes to Jolt, `System.Update` steps the solver, and `SyncEntityToJoltAction` copies simulated transforms and velocities back to components.
2. Outside `_solverLock`, `_contactBuffer.Drain` invokes `ContactResolved` on the physics tick thread (`JoltPhysicsSystem.cs:245`). Subscribers can query `Raycast` or `Overlaps` and mutate the `DataStore` without deadlocking `_solverLock`.

## Entity mapping and stale bodies

`SyncEntityToJoltAction` encodes each entity index into Jolt body `UserData` as `(ulong)entity + 1UL` when creating the body (`JoltPhysicsSystem.cs:303-306`, `:519`). Value `0` marks an unmapped or freed body.

`PhysicsComponent.Release` sets `UserData` to `0` through `BodyInterface` before it calls `RemoveAndDestroyBody` (`Swordfish/ECS/PhysicsComponent.cs:81-85`). `PhysicsComponentProcessor` also runs inside `StepSolver` before `System.Update` (`JoltPhysicsSystem.cs:233`). It destroys bodies freed during `FixedUpdate` or an earlier accumulated step before the next solver step runs.

`OnContactAdded` decodes `UserData` directly from each `Body` without touching `DataStore` (`JoltPhysicsSystem.cs:260-263`, `:308-319`). It ignores any contact that involves a body with `UserData` equal to `0`.

## Contact data and thread-safe buffering

Inside `OnContactAdded` (`JoltPhysicsSystem.cs:248-282`), Jolt worker threads read pre-impulse collision data before the constraint solver applies impulses:

- `Normal`: `manifold.WorldSpaceNormal`, pointing from `EntityA` toward `EntityB` (`Swordfish/Physics/PhysicsContactEvent.cs:23`).
- `ContactPoint`: average midpoint of `GetWorldSpaceContactPointOn1` and `GetWorldSpaceContactPointOn2` across all manifold points, or body midpoint when point count is `0` (`JoltPhysicsSystem.cs:284-301`).
- `RelativeVelocity`: pre-impulse `GetPointVelocity` of `EntityA` minus `EntityB` at `ContactPoint`, treating static bodies as zero velocity (`JoltPhysicsSystem.cs:267-269`).
- `ImpactSpeed`: `MathF.Max(0f, Vector3.Dot(relativeVelocity, normal))` (`JoltPhysicsSystem.cs:270`).

`JoltContactBuffer` (`Swordfish/Physics/Jolt/JoltContactBuffer.cs:7-80`) uses double-buffered pre-allocated `PhysicsContactEvent[]` arrays (`256` slots) with atomic `Interlocked.Increment` slot reservation on worker threads. Overflow beyond active capacity spills to a `ConcurrentQueue<PhysicsContactEvent>`, and `Drain` grows both arrays on the tick thread after dispatch (`JoltContactBuffer.cs:46-77`). Empty ticks return immediately with zero heap allocations.

## Source of truth

- Public physics contract and value types: `Swordfish/Physics/{IPhysics.cs:5-28,PhysicsContactEvent.cs:6-36,PhysicsContactKind.cs:4-11}`
- Jolt solver, listener, and buffer: `Swordfish/Physics/Jolt/{JoltPhysicsSystem.cs:48-116,227-319,516-532,JoltContactBuffer.cs:7-80}`
- Body UserData clear on disposal: `Swordfish/ECS/PhysicsComponent.cs:75-100`, `Swordfish/ECS/PhysicsComponentProcessor.cs:4-14`

## Tests that pin this

- `Swordfish.Tests/ContactPhysicsTests.cs`
- `Swordfish.Tests/RaycastPhysicsTests.cs`
- `Swordfish.Tests/ShapeOverlapPhysicsTests.cs`
- `Swordfish.Tests/PhysicsSyncIdleTests.cs`
- `Swordfish.Tests/ConvexHullPhysicsTests.cs`
- `Swordfish.Tests/ComponentResourceLifetimeTests.cs`
