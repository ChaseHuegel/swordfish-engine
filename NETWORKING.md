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
│   ├── network.nsd                          # ClientInputMsg, WorldSnapshotMsg, GamePacket, GamePacketType
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
├── Sessions/
│   ├── Session.cs                           # session ID + metadata struct
│   └── SessionService.cs                    # service for creating, validating, ending sessions
├── Transport/
│   ├── IDataReceiver.cs                     # raw bytes in from transport
│   ├── IDataSender.cs                       # raw bytes out to transport (per-session or filtered)
│   ├── IDataProducer.cs                     # coalesced, parsed byte segments
│   ├── IParser.cs                           # frame → packet boundary parser
│   ├── FrameStream.cs                       # length-prefixed frame reader/writer
│   ├── FrameStreamService.cs                # abstract base: per-peer threads, session→frame mapping
│   ├── TCPFrameServer.cs                    # TCP listener implementation (development use)
│   └── TCPFrameClient.cs                    # TCP connector implementation (development use)
├── Messaging/
│   ├── IMessageConsumer.cs                  # typed message subscription
│   ├── IMessageProducer.cs                  # typed message sending (session or broadcast)
│   ├── MessageConsumer.cs                   # generic deserialize + dispatch
│   ├── MessageProducer.cs                   # generic serialize + send
│   ├── PacketConsumer.cs                    # GamePacket-envelope-aware consumer (checks type)
│   └── PacketAwaiter.cs                     # await a specific response packet
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
│   ├── ServerInputSystem.cs                 # ClientInputMsg → InputComponent (triggered by MessageEventProcessor)
│   ├── OneShotCommandSystem.cs             # command packets → command components
│   └── NetworkReplicationSystem.cs          # DirtyComponent → WorldSnapshotMsg (sends via IMessageProducer)
├── SessionManager.cs                        # Session ↔ entity handle mapping
└── Processors/
    └── ClientInputProcessor.cs              # IEventProcessor<MessageEventArgs<ClientInputMsg>>

WaywardBeyond.Client.Core/
├── Systems/
│   ├── ClientInputSystem.cs                 # IInputService → InputComponent + send via IMessageProducer
│   └── ClientReconcileSystem.cs             # WorldSnapshotMsg → reconcile prediction
└── Networking/
    ├── GameClient.cs                        # transport selection (RUDP/TCP/LocalConnection) + DI wiring
    └── Processors/
        └── WorldSnapshotProcessor.cs        # IEventProcessor<MessageEventArgs<WorldSnapshotMsg>>
```

### Reference chains

```
WaywardBeyond.Shared.Networking
  └── references: Swordfish.ECS, Swordfish.Library, WaywardBeyond.Shared.Config

WaywardBeyond.Server.Core
  └── references: WaywardBeyond.Shared.Networking (add)

WaywardBeyond.Client.Core
  └── references: WaywardBeyond.Shared.Networking (add)

WaywardBeyond.Client.Core (singleplayer)
  └── uses LocalConnection — same interfaces, zero serialization, direct DataStore write
```

---

## Transport Layer

A layered transport abstraction sits between the raw network and the ECS. This decouples gameplay networking from transport specifics (TCP for dev, RUDP for production, named pipes for local).

### Interface layer

```csharp
// WaywardBeyond.Shared.Networking/Transport/IDataReceiver.cs
public interface IDataReceiver
{
    event EventHandler<DataEventArgs>? Received;
}

// WaywardBeyond.Shared.Networking/Transport/IDataSender.cs
public interface IDataSender
{
    Result Send(byte[] data, Session target);
    Result Send(byte[] data, IFilter<Session> targetFilter);
}

// WaywardBeyond.Shared.Networking/Transport/IDataProducer.cs
public interface IDataProducer
{
    event EventHandler<DataEventArgs>? Received;
}
```

`DataProducer` subscribes to all `IDataReceiver`s, runs incoming bytes through an `IParser` to split frame boundaries (e.g. length-prefixed), and re-emits complete byte segments as `DataEventArgs`. This means transport implementations only worry about pushing bytes; framing and coalescing are handled once.

```csharp
// WaywardBeyond.Shared.Networking/Transport/IParser.cs
public interface IParser
{
    List<byte[]> Parse(byte[] data);   // split raw bytes into complete packets
}
```

### FrameStream (length-prefixed framing)

Every message on the wire is prefixed with a 4-byte little-endian length (including the length field itself). This is used during development with TCP; in production the RUDP layer (`Currents/CRNT`) handles its own framing.

```csharp
// WaywardBeyond.Shared.Networking/Transport/FrameStream.cs
public sealed class FrameStream(Stream stream) : IDisposable
{
    public Result WriteFrame(byte[] data);
    public Result<byte[]> ReadFrame();
}
```

### Session-to-transport mapping

`FrameStreamService` (abstract) manages per-session `FrameStream` instances, each running on its own read loop via `ThreadWorker`. When a peer connects, a `Session` is created through `SessionService` and mapped to the frame stream. On disconnect, the session is cleaned up.

**Not used in singleplayer** — `LocalConnection` implements `IDataSender`/`IDataReceiver` directly, bypassing all framing and threads.

### Transport implementations

| Class | Role | When used |
|---|---|---|
| `TCPFrameServer` | `TcpListener` → `FrameStream` per client | Development, LAN |
| `TCPFrameClient` | Outgoing TCP `TcpClient` → `FrameStream` | Development client |
| `RUDPFrameServer` | `Currents` listener → framed receives | Production server |
| `RUDPFrameClient` | `Currents` client → framed receives | Production client |
| `LocalConnection` | Direct DataStore read/write, no bytes | Singleplayer |

The `GameClient` class (Phase 4) selects the appropriate transport on startup and exposes it as `IDataSender` + `IDataReceiver` — gameplay systems never reference TCP or RUDP directly.

---

## Wire Protocol — GamePacket Envelope

Every message sent over the transport layer is wrapped in a `GamePacket` envelope. This provides demux, ordering, and ack information independent of the transport underneath.

```csharp
// WaywardBeyond.Shared.Networking/Envelope/GamePacket.cs
public readonly struct GamePacket
{
    public readonly uint SequenceNumber;     // monotonic, per-session
    public readonly uint Ack;                // last sequence number received from peer
    public readonly GamePacketType Type;     // which message is in the payload
    public readonly byte[] Payload;          // serialized message body
}
```

```csharp
// WaywardBeyond.Shared.Networking/Envelope/GamePacketType.cs
public enum GamePacketType : ushort
{
    // Input (client → server)
    ClientInput     = 100,

    // Replication (server → client)
    WorldSnapshot   = 200,

    // One-shot commands (client → server)
    PlaceBlock      = 300,
    BreakBlock      = 301,

    // Session lifecycle
    LoginRequest    = 400,
    LoginResponse   = 401,
    JoinRequest     = 402,
    JoinResponse    = 403,
    Logout          = 404,
}
```

### How the envelope is used

```
[transport bytes] → IParser.Parse() → [complete byte segments]
    → DataProducer → DataEventArgs
        → PacketConsumer<T> → checks GamePacket.Type matches serializer's type
            → strips envelope, deserializes payload → MessageEventArgs<T>
```

The `PacketConsumer<T>` reads the `GamePacket` envelope, verifies the `Type` field matches its registered type before attempting to deserialize, and discards mismatched packets. This prevents one misbehaving sender from causing deserialization exceptions in unrelated consumers.

### Sequence numbers and acknowledgments

Each `GamePacket` carries a monotonic `SequenceNumber` (per-session direction) and the `Ack` field acknowledging the last packet received from the peer. This allows:

- **Duplicate detection** — drop packets with `SequenceNumber ≤ lastReceived`
- **RTT estimation** — `now - lastAckWallClock` when `Ack` advances
- **Reliable delivery** — the transport layer (RUDP) has its own ack at the frame level; the envelope ack is an additional layer for tracking snapshot delivery at the gameplay level

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
    public uint ServerTickAtSample;      // server tick when input was sampled; also echoes last applied snapshot tick (snapshot ack)
}
```

### `NetworkComponent` — Identity mapping

```csharp
public struct NetworkComponent : IDataComponent
{
    public Session Session;              // connected client session (from SessionService)
    public uint LastAckedInput;          // last input sequence the server processed
    public uint LastAckedSnapshot;       // last snapshot sequence the client processed
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

## Session Lifecycle

Session management is handled by `SessionService` — separate from the ECS but referenced by `NetworkComponent.Session`. This keeps connection lifecycle logic out of systems while still allowing ECS queries to filter by session.

```csharp
// WaywardBeyond.Shared.Networking/Session/Session.cs
public readonly struct Session(uint id) : IEquatable<Session>
{
    public readonly uint ID = id;
    // Equality operators, GetHashCode, etc.
}

// WaywardBeyond.Shared.Networking/Session/SessionService.cs
public class SessionService
{
    public Result<Session> RequestNew();           // allocate a new session ID
    public Result<Session> End(Session session);   // close and release
    public Result<Session> Validate(Session session);  // still alive?
    public Result<Session> Get(uint id);
}
```

### Lifecycle flow

1. **Connection** — Transport layer (`FrameStreamService`) detects new peer → calls `SessionService.RequestNew()` → receives `Session` → maps it to the `FrameStream` internally
2. **Mapping** — Server entity that represents the player gets `NetworkComponent.Session = session` and is registered in a `Dictionary<Session, int>` lookup from `SessionManager`
3. **Tick** — `NetworkReplicationSystem` checks which entities have `NetworkComponent.Session` matching connected sessions
4. **Disconnect** — Transport detects peer gone → calls `SessionService.End(session)` → `SessionManager` removes the mapping → entity may be deleted or flagged for cleanup

### Singleplayer path

In singleplayer, `LocalConnection` uses a sentinel `Session` with `ID = 0`. `SessionService.RequestNew()` is never called — the local player entity is created directly with `Session = new Session(0)` and the transport layer is entirely bypassed.

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
    <Exec Command="nsdc -r -p -i CodeGen -o ./CodeGen/Output"/>
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

### Transport data flow (all frames)

```
[transport bytes arrive]
    → FrameStream.ReadFrame() → [complete frame]
    → DataProducer (via IParser) → [complete byte segments]
    → PacketConsumer<T>: checks GamePacket.Type matches
        → deserializes payload → MessageEventArgs<T>
    → MessageEventProcessor<TMessage>: routes to IEventProcessor<T>[]
```

Outbound goes the reverse:

```
MessageProducer<T>.Send(message, session)
    → ISerializer<T>.Serialize(message) → [payload bytes]
    → wraps in GamePacket envelope (sequence, ack, type)
    → FrameStream.WriteFrame(packetBytes)
    → transport send (TCP, RUDP, or LocalConnection)
```

### Client frame (every ECS tick)

```
ClientInputSystem (reads IInputService)
  ↓ writes InputComponent on local player entity
  ↓ stores copy in PendingInputComponent.History[]
  ↓ sends ClientInputMsg via IMessageProducer<ClientInputMsg>
      → GamePacket envelope → IDataSender → transport

PlayerControllerSystem (same code as server — prediction)
  ↓ reads InputComponent, writes PhysicsComponent, TransformComponent
  ↓ this is the client's predicted state

... render from predicted state ...
```

### Server frame (every ECS tick)

```
[transport → PacketConsumer<ClientInputMsg> → MessageEventProcessor triggers]

ServerInputSystem (IEventProcessor<MessageEventArgs<ClientInputMsg>>)
  ↓ converts ClientInputMsg → InputComponent on server entity
  ↓ stores LastAckedInput + LastAckedSnapshot (snapshot ack from client)
  ↓ in NetworkComponent

PlayerControllerSystem (same code as client — authoritative)
  ↓ reads InputComponent, writes PhysicsComponent, TransformComponent
  ↓ this is the server's authoritative state

Other gameplay systems run (physics, AI, etc.)
  ↓ each sets DirtyComponent bits when they mutate networked components

NetworkReplicationSystem
  ↓ queries NetworkComponent + DirtyComponent
  ↓ checks LastAckedSnapshot to skip unchanged entities
  ↓ serializes only flagged components into WorldSnapshotMsg
  ↓ sends via IMessageProducer<WorldSnapshotMsg>
      → GamePacket envelope → IDataSender → transport
  ↓ clears DirtyComponent
```

### Client receive (asynchronous, not on ECS tick)

```
[transport → PacketConsumer<WorldSnapshotMsg> → MessageEventProcessor triggers]

ClientReconcileSystem (IEventProcessor<MessageEventArgs<WorldSnapshotMsg>>)
  ↓ finds local entity by Session (not entity ID)
  ↓ reads LastProcessedInput + TickNumber from snapshot
  ↓ walks PendingInputComponent.History:
      - drops inputs ≤ LastProcessedInput (acknowledged)
      - restores entity state to server snapshot position
      - re-applies remaining unacknowledged inputs
  ↓ writes corrected TransformComponent, PhysicsComponent
  ↓ echoes TickNumber in next ClientInputMsg.ServerTickAtSample (snapshot ack)
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
3. Client updates `NetworkComponent.LastAckedSnapshot = snapshot.TickNumber` and includes this in the next `ClientInputMsg` as an implicit snapshot ack
4. For remaining inputs (`SequenceNumber > N`):
   a. Overwrite entity's `TransformComponent`/`PhysicsComponent` with snapshot values
   b. Re-run the replayed inputs in order through `PlayerControllerSystem`
5. Entity state now matches what the server will compute for those inputs

### Ring buffer sizing

- 256 entries at 30 Hz (~8.5 seconds) is a comfortable default
- Increase if high latency is expected

## Snapshot Acknowledgment

Beyond input acknowledgment, the server needs to know which snapshots the client has received. This prevents:

- **Re-sending unchanged data** — server tracks `LastAckedSnapshot` per `NetworkComponent`; if no components changed since the last acked tick, skip the entity
- **Bandwidth waste** — server uses ack to throttle snapshot rate when the client's receive window is full

### How it works

1. Every `WorldSnapshotMsg` carries a `TickNumber` (monotonic)
2. Client echoes the highest `TickNumber` it has processed in the next `ClientInputMsg.ServerTickAtSample` field (which already exists for prediction timing)
3. Server reads this field as a snapshot ack: `NetworkComponent.LastAckedSnapshot = clientMsg.ServerTickAtSample`
4. `NetworkReplicationSystem` uses `LastAckedSnapshot` to skip entities whose `DirtyComponent` was last dirtied before that tick

```csharp
// In NetworkReplicationSystem
store.Query<NetworkComponent, DirtyComponent>(delta, (d, s, entity, ref net, ref dirty) =>
{
    if (dirty.LastDirtyTick <= net.LastAckedSnapshot)
        return;  // client already has this state

    // ... serialize and send delta ...
});
```

### Dual ack channels

| Ack type | Field | Direction | Purpose |
|---|---|---|
| Input ack | `NetworkComponent.LastAckedInput` | Server → Client (in snapshot) | Trim prediction history |
| Snapshot ack | `NetworkComponent.LastAckedSnapshot` | Client → Server (echoed in input) | Skip redundant replication |

---

## Interest Management

The architecture above assumes the server sends all relevant entities to all clients — acceptable for small-scale (<64 entities). For the voxel space RPG target, interest management is deferred to a later phase.

### Planned approach: Spatial grid (AOI)

- Divide the world into sectors (e.g., 500m cubes)
- Each sector tracks which entities are inside it
- Server maintains per-client `IFilter<Session>` that includes only sectors within a configurable radius of the player
- `IDataSender.Send(data, IFilter<Session>)` already supports target filtering — the filter implementation becomes AOI-aware
- `NetworkComponent` reserves a bitfield for sector membership, updated by a dedicated `AreaOfInterestSystem`

### What stays the same

- All ECS replication code is unchanged — only the filter passed to `Send()` changes
- `DirtyComponent` still tracks what changed; AOI decides who needs to know
- Transport layer's `IDataSender.Send(data, IFilter<Session>)` is the extension point

### Not in Phase 1

Interest management is **not part of the initial implementation**. Phase 1 targets a fully-connected broadcast model. AOI is added when player counts exceed ~64 and bandwidth becomes measurable.

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

### Phase 1 — Transport Layer
1. Create `WaywardBeyond.Shared.Networking` project (`.csproj`, framework references)
2. Implement `Session` struct and `SessionService` (allocate, validate, end, get)
3. Implement transport interfaces: `IDataReceiver`, `IDataSender`, `IDataProducer`, `IParser`
4. Implement `DataProducer` (subscribes to receivers, parses, re-emits)
5. Implement `FrameStream` (length-prefixed framing via Stream)
6. Implement `FrameStreamService` (abstract base: per-session read loops, session→frame mapping)
7. Implement `TCPFrameServer` and `TCPFrameClient` (development transport)
8. Implement `GamePacket` envelope (`SequenceNumber`, `Ack`, `Type`, `Payload`)
9. Implement `IMessageConsumer<T>`, `IMessageProducer<T>`, `MessageConsumer<T>`, `MessageProducer<T>`
10. Implement `PacketConsumer<T>` (checks `GamePacket.Type` before deserializing)
11. Implement `PacketAwaiter<T>` (one-shot await for response packets)

### Phase 2 — Foundation
12. Add `CodeGen/network.nsd` with `ClientInputMsg`, `EntitySnapshotMsg`, `WorldSnapshotMsg`
13. Add `RunCodeGen` target to `.csproj`
14. Implement `NetworkRegistry` (type→bit mapping, thread-safe)
15. Implement `DirtyComponent` (256-bit bitset, `SetDirty<T>()`, `Clear()`, `Any()`, `ForEachDirty()`)
16. Implement `NetworkComponent` (includes `Session`, `LastAckedInput`, `LastAckedSnapshot`, `ServerTPS`)
17. Implement extension method `RegisterNetworkComponent<T>()`
18. Add project references from `Server.Core` and `Client.Core`

### Phase 3 — Shared ECS Components
19. Implement `InputComponent` (movement, look, jump, sequence number, snapshot ack field)
20. Implement `PlaceBlockCommand`, `BreakBlockCommand` (one-shot markers)
21. Implement `PendingInputComponent` (ring buffer for client prediction)
22. Implement conversion layer (`EntitySnapshotExtensions`)

### Phase 4 — Server Systems
23. Implement `SessionManager` (maps `Session` ↔ entity handle)
24. Implement `ServerInputSystem` (receives `ClientInputMsg` → writes `InputComponent`, updates `LastAckedInput` and `LastAckedSnapshot`)
25. Implement `NetworkReplicationSystem` (reads dirty components → builds `WorldSnapshotMsg`, respects `LastAckedSnapshot`)
26. Wire into server's `Injector.cs`

### Phase 5 — Client Systems
27. Implement `GameClient` with `IsLocal` flag and bidirectional RUDP (or `LocalConnection`)
28. Implement `ClientInputSystem` (reads `IInputService` → writes `InputComponent` + sends via transport)
29. Implement `ClientReconcileSystem` (receives snapshots → reconciles prediction, echoes snapshot ack)
30. Wire into client's `Injector.cs`

### Phase 6 — Integration
31. Implement `LocalConnection` for singleplayer (direct DataStore write, no serialization, sentinel Session ID=0)
32. Add `RegisterNetworkComponent<T>()` calls in existing modules for components that need replication
33. Add `MarkDirty<T>()` calls to existing systems that mutate networked components
34. Test singleplayer (zero regression — same as before, but now going through the networking abstraction)
35. Test multiplayer (two clients, one server, latency simulation)

### Phase 7 — Optimization (deferred)
36. Implement RUDP transport (Currents/CRNT `RUDPFrameServer`/`RUDPFrameClient`)
37. Implement AOI interest management (spatial grid filter on `IDataSender.Send`)
38. Implement snapshot delta compression (skip unchanged entities per `LastAckedSnapshot`)

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
| Transport layering | `IDataReceiver`/`IDataSender`/`IDataProducer`/`IParser` pipeline | Decouples gameplay from transport; TCP for dev, RUDP for prod, LocalConnection for singleplayer — all via the same interfaces |
| Wire envelope | `GamePacket` (sequence, ack, type, payload) on every message | Provides demux, ordering, and ack at the gameplay level independent of transport |
| Session lifecycle | `SessionService` (create/validate/end) + `Session` in `NetworkComponent` | Separates connection management from ECS; clean disconnect path; maps cleanly to `IDataSender.Send(data, IFilter<Session>)` |
| Snapshot ack | `LastAckedSnapshot` echoed in `ClientInputMsg.ServerTickAtSample` | Lets server skip unchanged entities; dual ack channels (input + snapshot) are independent |
| Authority | Server authoritative | Simplifies anti-cheat, eliminates sync conflicts |
| Prediction | Client runs same `PlayerControllerSystem` + reconciles on snapshot | No duplicate game logic; reuse existing systems |
| Tick rate | Not shared — server advertises, client optionally matches | Space flight prediction drifts minimally; reconciliation fixes any error |
| Transport | RUDP (Currents/CRNT) for production client-server; TCP for dev | AGENTS.md lists Currents as existing dependency; abstraction makes transport swappable |
| Interest mgmt | AOI spatial grid — deferred to Phase 7 | Broadcast is fine for <64 entities; AOI adds bandwidth savings at scale |
| Inter-server | NATS JetStream (existing `PacketStreamClient`) | Already built and tested |
| Serialization | Needlefish (.nsd schemas) for network DTOs | Consistent with rest of codebase; auto-generated code |
| Singleplayer | LocalConnection (direct DataStore, no serialization, sentinel Session ID=0) | Same systems, same ECS, zero overhead |
| Mod networking | Explicit registration per module via `RegisterNetworkComponent<T>()` | Predictable, validated at startup, no runtime magic |
