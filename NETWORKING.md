# Swordfish Engine — Networking Architecture & Implementation Plan

## Overview

Architecture for multiplayer-ready networking in an ECS-based space RPG voxel game. Built on component-delta replication with an authoritative server, client-side prediction, and a two-tier input system.

### Core Principles

- **ECS-first** — networking is expressed through components and systems, not external event busses or command objects
- **Same code, client and server** — gameplay systems (`PlayerControllerSystem`, etc.) are shared; only the transport/sync layer differs
- **Delta replication** — only changed components are serialized and sent each tick
- **Authoritative server** — the server's ECS is the source of truth; clients predict and reconcile
- **Singleplayer = local server** — same architecture, no serialization, same ECS tick
- **Structs by default** — all network data are `readonly struct` or `IDataComponent` where possible
- **Explicit registration** — mods register networked components via DI at startup

---

## Project Structure

### New project

```
WaywardBeyond.Shared.Networking/            (new, net9.0)
├── CodeGen/
│   ├── network.nsd                          # ClientInputMsg, WorldSnapshotMsg
│   └── Output/                              # generated (nsdc)
├── Components/
│   ├── InputComponent.cs                    # continuous player input
│   ├── NetworkComponent.cs                  # entity ↔ client mapping
│   ├── DirtyComponent.cs                    # 256-bit change mask
│   └── PendingInputComponent.cs             # client-side input history (ring buffer)
├── Commands/
│   ├── PlaceBlockCommand.cs                 # one-shot action marker
│   └── BreakBlockCommand.cs                 # one-shot action marker
├── Registry/
│   └── NetworkRegistry.cs                   # component type → bit mapping
├── Systems/
│   └── NetworkedSystem.cs                   # optional base class with MarkDirty<T>()
├── Snapshots/
│   └── EntitySnapshot.cs                    # conversion: ECS ↔ .nsd DTOs
└── WaywardBeyond.Shared.Networking.csproj
```

### Enhanced existing projects

```
WaywardBeyond.Server.Core/
├── Systems/
│   ├── ServerInputSystem.cs                 # ClientInputMsg → InputComponent
│   ├── OneShotCommandSystem.cs             # command packets → command components
│   └── NetworkReplicationSystem.cs          # DirtyComponent → WorldSnapshotMsg
└── SessionManager.cs                        # client connection ↔ entity mapping

WaywardBeyond.Client.Core/
├── Systems/
│   ├── ClientInputSystem.cs                 # IInputService → InputComponent + send
│   └── ClientReconcileSystem.cs             # WorldSnapshotMsg → reconcile prediction
└── Networking/
    └── GameClient.cs                        # RUDP connection + singleplayer fallback
```

### Reference chains

```
WaywardBeyond.Shared.Networking
  └── references: Swordfish.ECS, Swordfish.Library, WaywardBeyond.Shared.Config

WaywardBeyond.Server.Core
  └── references: WaywardBeyond.Shared.Networking (add)

WaywardBeyond.Client.Core
  └── references: WaywardBeyond.Shared.Networking (add)
```

---

## Shared Components (manual structs, not .nsd)

### `InputComponent` — Continuous player input (one per player entity)

```csharp
public struct InputComponent : IDataComponent
{
    public Vector3 Movement;             // normalized direction
    public Vector2 LookDelta;            // mouse delta this frame
    public bool Jump;
    public uint SequenceNumber;          // monotonic counter for prediction
    public uint ServerTickAtSample;      // server tick when input was sampled
}
```

### `NetworkComponent` — Identity mapping

```csharp
public struct NetworkComponent : IDataComponent
{
    public uint NetworkID;               // maps to a connected client session
    public uint LastAckedInput;          // last input sequence the server processed
    public uint ServerTPS;               // server's current tick rate (advertised)
}
```

### `DirtyComponent` — Change tracking (256-bit bitset)

```csharp
public struct DirtyComponent : IDataComponent
{
    private unsafe fixed ulong Bits[4];  // 256 bits

    public void SetDirty<T>() where T : struct, IDataComponent
    {
        int bit = NetworkRegistry.GetBit<T>();
        // set the bit
    }

    public bool IsDirty<T>() where T : struct, IDataComponent { ... }
    public void Clear() { }              // zero the bits
    public bool Any() { }                // true if any bit is set
    internal readonly unsafe void ForEachDirty(Action<int> onDirty) { }
}
```

### `PendingInputComponent` — Client prediction ring buffer

```csharp
public struct PendingInputComponent : IDataComponent
{
    public InputComponent[] History;     // circular buffer
    public uint Head;                    // write index
    public uint Tail;                    // oldest unacknowledged index
}
```

### One-shot command components

```csharp
public struct PlaceBlockCommand : IDataComponent
{
    public Int3 Position;
    public string BrickID;               // or int ID for performance
}

public struct BreakBlockCommand : IDataComponent
{
    public Int3 Position;
}
```

---

## Network Messages (.nsd schemas)

### `WaywardBeyond.Shared.Networking/CodeGen/network.nsd`

```nsd
#version 1.0;
#namespace WaywardBeyond.Shared.Networking;

message ClientInputMsg
{
    uint SequenceNumber   = 0;
    float MovementX       = 1;
    float MovementY       = 2;
    float MovementZ       = 3;
    float LookDeltaX      = 4;
    float LookDeltaY      = 5;
    bool Jump             = 6;
    bool PrimaryAction    = 7;
    bool SecondaryAction  = 8;
    uint ServerTickAtSample = 9;
}

message EntitySnapshotMsg
{
    uint NetworkID        = 0;
    float PositionX       = 1;
    float PositionY       = 2;
    float PositionZ       = 3;
    float OrientationX    = 4;
    float OrientationY    = 5;
    float OrientationZ    = 6;
    float OrientationW    = 7;
    float VelocityX       = 8;
    float VelocityY       = 9;
    float VelocityZ       = 10;
}

message WorldSnapshotMsg
{
    uint TickNumber          = 0;
    uint LastProcessedInput  = 1;
    EntitySnapshotMsg[] Entities = 2;
}
```

Add the `RunCodeGen` target to the `.csproj` (same pattern as `WaywardBeyond.Shared.Data`):

```xml
<PropertyGroup>
    <DefaultItemExcludes>$(DefaultItemExcludes);CodeGen/Output/**/*.cs</DefaultItemExcludes>
</PropertyGroup>
<Target Name="RunCodeGen" BeforeTargets="CoreCompile">
    <Exec Command="nsdc -r -i CodeGen -o ./CodeGen/Output"/>
    <ItemGroup>
        <Compile Include="CodeGen/Output/**/*.cs"/>
    </ItemGroup>
</Target>
```

### Conversion layer (manual, not generated)

```csharp
// WaywardBeyond.Shared.Networking/Snapshots/EntitySnapshot.cs
public static partial class EntitySnapshotExtensions   // partial to coexist with generated
{
    public static ClientInputMsg ToMessage(in this InputComponent input, uint serverTick)
    {
        return new ClientInputMsg
        {
            SequenceNumber = input.SequenceNumber,
            MovementX = input.Movement.X,
            MovementY = input.Movement.Y,
            MovementZ = input.Movement.Z,
            // ...
        };
    }

    public static void ApplyToStore(in this WorldSnapshotMsg snapshot, DataStore store)
    {
        for (var i = 0; i < snapshot.Entities.Length; i++)
        {
            EntitySnapshotMsg entity = snapshot.Entities[i];
            // Find entity by NetworkID and apply position/velocity
        }
    }
}
```

---

## `NetworkRegistry` — Type-to-bit mapping

```csharp
// WaywardBeyond.Shared.Networking/Registry/NetworkRegistry.cs
public static class NetworkRegistry
{
    internal const int MaxComponents = 256;
    private static readonly Dictionary<Type, int> _map = [];
    private static int _next;
    private static readonly Lock _lock = new();

    public static void Register<T>() where T : struct, IDataComponent
    {
        lock (_lock)
        {
            if (_map.ContainsKey(typeof(T))) return;
            int bit = _next++;
            if (bit >= MaxComponents)
                throw new InvalidOperationException($"Registry exceeded {MaxComponents} components.");
            _map[typeof(T)] = bit;
        }
    }

    internal static int GetBit<T>() where T : struct, IDataComponent
    {
        return _map[typeof(T)];
    }
}
```

### DI extension for modules

```csharp
// WaywardBeyond.Shared.Networking/Extensions/ContainerExtensions.cs
public static class ContainerExtensions
{
    public static void RegisterNetworkComponent<T>(this IContainer container)
        where T : struct, IDataComponent
    {
        NetworkRegistry.Register<T>();
    }
}
```

Usage in a mod's `Injector.cs`:

```csharp
container.RegisterNetworkComponent<MyModWeaponComponent>();
container.RegisterNetworkComponent<MyModShieldComponent>();
```

Registration happens at module load (before gameplay starts) so bit assignments are stable.

---

## System Pipeline

### Client frame (every ECS tick)

```
ClientInputSystem (reads IInputService)
  ↓ writes InputComponent on local player entity
  ↓ stores copy in PendingInputComponent.History[]
  ↓ sends ClientInputMsg over RUDP

PlayerControllerSystem (same code as server — prediction)
  ↓ reads InputComponent, writes PhysicsComponent, TransformComponent
  ↓ this is the client's predicted state

... render from predicted state ...
```

### Server frame (every ECS tick)

```
RUDP receive → deserialize ClientInputMsg

ServerInputSystem
  ↓ converts ClientInputMsg → InputComponent on server entity
  ↓ stores LastAckedInput in NetworkComponent

PlayerControllerSystem (same code as client — authoritative)
  ↓ reads InputComponent, writes PhysicsComponent, TransformComponent
  ↓ this is the server's authoritative state

Other gameplay systems run (physics, AI, etc.)
  ↓ each sets DirtyComponent bits when they mutate networked components

NetworkReplicationSystem
  ↓ queries NetworkComponent + DirtyComponent
  ↓ serializes only flagged components into WorldSnapshotMsg
  ↓ sends over RUDP
  ↓ clears DirtyComponent
```

### Client receive (asynchronous, not on ECS tick)

```
RUDP receive → deserialize WorldSnapshotMsg

ClientReconcileSystem
  ↓ finds local entity by NetworkID
  ↓ reads LastProcessedInput from snapshot
  ↓ walks PendingInputComponent.History:
      - drops inputs ≤ LastProcessedInput (acknowledged)
      - restores entity state to server snapshot position
      - re-applies remaining unacknowledged inputs
  ↓ writes corrected TransformComponent, PhysicsComponent
```

---

## Client Prediction & Reconciliation Detail

### Prediction flow

1. Each frame the client samples input → appends to `PendingInputComponent.History` ring buffer
2. `PlayerControllerSystem` runs immediately with the latest input (predicted state)
3. Client renders from predicted state

### Reconciliation flow

1. Server snapshot arrives with `LastProcessedInput = N`
2. All inputs with `SequenceNumber ≤ N` are acknowledged — drop from history
3. For remaining inputs (`SequenceNumber > N`):
   a. Overwrite entity's `TransformComponent`/`PhysicsComponent` with snapshot values
   b. Re-run the replayed inputs in order through `PlayerControllerSystem`
4. Entity state now matches what the server will compute for those inputs

### Ring buffer sizing

- 256 entries at 30 Hz (~8.5 seconds) is a comfortable default
- Increase if high latency is expected

---

## Singleplayer Path

No RUDP connection. A `LocalConnection` implements the same transport interface but:

- `Send(msg)` → directly applies `ClientInputMsg` to the local server's DataStore (no serialization, no network)
- `Receive()` → returns empty, since the client is already reading from authoritative state
- `NetworkReplicationSystem` is registered but sees zero clients → no snapshots sent
- `ClientReconcileSystem` is a no-op (nothing to reconcile)
- `PlayerControllerSystem` runs once on the shared ECS — authoritative by definition

Gated by a flag:

```csharp
internal sealed class GameClient
{
    public bool IsLocal { get; }  // true when singleplayer
    // ...
}
```

`ClientInputSystem` checks this to decide direct write vs RUDP send.

---

## Tick Rate

### Decision: Do not enforce shared tick rate

| Reason | Detail |
|---|---|
| **Space flight prediction** | Inertia-based movement drifts minimally at different tick rates |
| **Server is authoritative** | Reconciliation corrects any drift regardless of rate mismatch |
| **Hardware diversity** | Client can run at native refresh; server at fixed rate |
| **Existing flexibility** | `ThreadWorker.TargetTickRate` is already dynamic |

### What to implement

1. Server advertises its current TPS in `NetworkComponent.ServerTPS` during connection handshake
2. Client uses this as a **recommended** prediction rate (not enforced)
3. If drift is later measured as problematic, add `ThreadWorker.TargetTickRate = advertisedTPS` — one line

---

## `DirtyComponent` — How Systems Use It

### Writing (systems that mutate networked state)

```csharp
protected override void OnTick(float delta, DataStore store, int entity, ref PhysicsComponent physics)
{
    physics.Velocity = newVelocity;
    store.AddOrUpdate(entity, new DirtyComponent { Dirty = ... });  // manual
    // OR via helper:
    this.MarkDirty<PhysicsComponent>(store, entity);                // via NetworkedSystem base
}
```

### Helper method (on `NetworkedSystem` base class or extension)

```csharp
public static void MarkDirty<T>(this DataStore store, int entity) where T : struct, IDataComponent
{
    if (!store.TryGet(entity, out DirtyComponent dirty))
        return;
    dirty.SetDirty<T>();
    store.AddOrUpdate(entity, dirty);
}
```

### Reading (NetworkReplicationSystem)

```csharp
store.Query<NetworkComponent, DirtyComponent>(delta, (d, s, entity, ref net, ref dirty) =>
{
    if (!dirty.Any())
        return;

    var snapshot = new WorldSnapshotMsg { ... };
    dirty.ForEachDirty(bit =>
    {
        Type componentType = NetworkRegistry.GetType(bit);  // reverse lookup
        // serialize component, add to snapshot
    });

    Send(snapshot);
    dirty.Clear();
    s.AddOrUpdate(entity, dirty);
});
```

---

## Implementation Order

### Phase 1 — Foundation
1. Create `WaywardBeyond.Shared.Networking` project (`.csproj`, framework references)
2. Add `CodeGen/network.nsd` with `ClientInputMsg`, `EntitySnapshotMsg`, `WorldSnapshotMsg`
3. Add `RunCodeGen` target to `.csproj`
4. Implement `NetworkRegistry` (type→bit mapping, thread-safe)
5. Implement `DirtyComponent` (256-bit bitset, `SetDirty<T>()`, `Clear()`, `Any()`, `ForEachDirty()`)
6. Implement `NetworkComponent`
7. Implement extension method `RegisterNetworkComponent<T>()`
8. Add project references from `Server.Core` and `Client.Core`

### Phase 2 — Shared ECS Components
9. Implement `InputComponent` (movement, look, jump, sequence number)
10. Implement `PlaceBlockCommand`, `BreakBlockCommand` (one-shot markers)
11. Implement `PendingInputComponent` (ring buffer for client prediction)
12. Implement conversion layer (`EntitySnapshotExtensions`)

### Phase 3 — Server Systems
13. Implement `SessionManager` (maps client connections ↔ entity `NetworkID`)
14. Implement `ServerInputSystem` (receives `ClientInputMsg` → writes `InputComponent`)
15. Implement `NetworkReplicationSystem` (reads dirty components → builds `WorldSnapshotMsg`)
16. Wire into server's `Injector.cs`

### Phase 4 — Client Systems
17. Implement `GameClient` with `IsLocal` flag and bidirectional RUDP (or `LocalConnection`)
18. Implement `ClientInputSystem` (reads `IInputService` → writes `InputComponent` + sends)
19. Implement `ClientReconcileSystem` (receives snapshots → reconciles prediction)
20. Wire into client's `Injector.cs`

### Phase 5 — Integration
21. Implement `LocalConnection` for singleplayer (direct DataStore write, no serialization)
22. Add `RegisterNetworkComponent<T>()` calls in existing modules for components that need replication
23. Add `MarkDirty<T>()` calls to existing systems that mutate networked components
24. Test singleplayer (zero regression — same as before, but now going through the networking abstraction)
25. Test multiplayer (two clients, one server, latency simulation)

---

## What Does NOT Change

- `Swordfish.ECS/DataStore.cs` — stays generic, no dirty flag support
- `Swordfish.ECS/EntitySystem.cs` — unchanged, though `NetworkedSystem` may be added as an optional convenience base
- `IEventProcessor<T>` / `EventInvoker<TEvent>` — still used for XP, level-ups, statistics, UI notifications (cross-cutting reactions that fire on the server and whose effects flow through component changes)
- `PacketStreamClient` + NATS JetStream — still used for inter-server packet distribution (not replaced by this architecture)
- `KeyValueStore` / `NatsCharacterStorage` — still used for persistence
- `PersistentNatsProcess` — still starts the local NATS server

---

## Modding

Each module declares its networked components explicitly:

```csharp
// MyVehicleMod/Injector.cs
public void Inject(IContainer container)
{
    container.RegisterNetworkComponent<EngineComponent>();
    container.RegisterNetworkComponent<ShieldComponent>();
    container.RegisterNetworkComponent<WeaponComponent>();
}
```

The `NetworkRegistry` validates at startup:
- Duplicate registrations are no-ops (same type registered twice from different modules)
- Overflow past 256 types throws immediately with a clear message
- Mods cannot steal or overlap another mod's bits

Modules also implement their own systems that call `MarkDirty<T>()` — the `NetworkReplicationSystem` automatically picks up any `DirtyComponent` bits regardless of which module set them.

---

## Key Design Decisions Reference

| Decision | Choice | Why |
|---|---|---|
| Input model | Two-tier: continuous `InputComponent` + one-shot command components | Keeps hot path tight, complex actions isolated |
| Change tracking | `DirtyComponent` with 256-bit bitset + explicit registration | ECS-idiomatic, cache-friendly, no changes to DataStore |
| Replication | Delta-only (changed components only) | Minimizes bandwidth; dirty flag makes detection trivially cheap |
| Authority | Server authoritative | Simplifies anti-cheat, eliminates sync conflicts |
| Prediction | Client runs same `PlayerControllerSystem` + reconciles on snapshot | No duplicate game logic; reuse existing systems |
| Tick rate | Not shared — server advertises, client optionally matches | Space flight prediction drifts minimally; reconciliation fixes any error |
| Transport | RUDP (Currents/CRNT) for client-server | AGENTS.md lists Currents as existing dependency; RUDP gives reliable + unordered channels |
| Inter-server | NATS JetStream (existing `PacketStreamClient`) | Already built and tested |
| Serialization | Needlefish (.nsd schemas) for network DTOs | Consistent with rest of codebase; auto-generated code |
| Singleplayer | LocalConnection (direct DataStore, no serialization) | Same systems, same ECS, zero overhead |
| Mod networking | Explicit registration per module via `RegisterNetworkComponent<T>()` | Predictable, validated at startup, no runtime magic |
