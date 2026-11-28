# Networking — Particle Effects

One subject: generic server-to-client replication of continuous particle emitters and batched one-shot particle bursts.

## Replicated continuous emitters

Continuous emitters replicate through `NetworkReplicationSystem` as `ServerOwned` components (`WaywardBeyond.Networking/NetworkingComposition.cs:50-51`).

- **`ParticleEmitterComponent` (Uuid `18`):** Uses `ParticleEmitterCodec` (`WaywardBeyond.Networking/Codecs/ParticleEmitterCodec.cs:11-155`) to serialize `ParticleEmitterMessage`. The codec resolves `EffectId` from `EffectId` or fallback `Effect.ID`. It enforces a non-empty, non-whitespace string up to `MAX_EFFECT_ID_LENGTH` (`128`), a finite non-negative `RateScale`, and finite `Offset` and `LocalDirection` vectors. Unchanged wire payloads skip `DataStore.AddOrUpdate` on clients so dirty flags stay clean.
- **`ParticleEmitterBindingComponent` (Uuid `19`):** Uses `NsdComponentCodec<ParticleEmitterBindingComponent>` (`WaywardBeyond.Data/Components/ParticleEmitterBindingComponent.cs:7-11`). `ParentEntity` attaches the emitter locally to a replicated parent entity.

## Server particle API

`IServerParticleService` (`WaywardBeyond.Server/Particles/IServerParticleService.cs:10-46`) is implemented by `ServerParticleService` (`WaywardBeyond.Server/Particles/ServerParticleService.cs:21-334`). `ServerWorld` resolves and pins one isolated instance per world (`WaywardBeyond.Server/ServerWorld.cs:52-54`).

- **`SpawnEmitter`:** Validates the emitter and transform, allocates a networked entity tagged `"game"`, sets `NetworkComponent.ExcludedRecipientEntity` to `excludeEntity`, writes `TransformComponent` and normalized `ParticleEmitterComponent`, and adds `ParticleEmitterBindingComponent` when `parentEntity` is non-null. `NetworkReplicationSystem` omits the entire entity (deltas, full sync, removals, and despawns) from the excluded recipient.
- **`DespawnEmitter`:** Checks `entity != Entity.Null`, non-null `expectedUuid`, and slot ownership before forwarding `NetworkComponent.ExcludedRecipientEntity` to `NetworkReplicationSystem.RequestDespawn` and freeing the entity.
- **`EmitBurst`:** Validates `EffectId`, finite `Position` and `Direction`, finite non-negative `SpeedScale`, finite positive `SizeScale`, and positive optional `Count`. Valid bursts enter a lock-guarded queue capped at `MAX_BURSTS_PER_TICK` (`128`). Excess bursts return failure and log once per tick.
- **Tick flush:** `ServerParticleFlushSystem` (`WaywardBeyond.Server/Systems/ServerParticleFlushSystem.cs:7-16`) calls `ServerParticleService.Flush()` right before `NetworkPublishSystem` (`WaywardBeyond.Server/ServerComposition.cs:132`). `Flush` skips clients with an active join stream (`NetworkReplicationSystem.IsStreaming`), reuses exact-length batch buffers for both shared and per-peer `ExcludeClient` sends, and closes any peer whose reliable send queue rejects the frame.

## Batched burst RPC

Queued server bursts travel as one `ParticleBurstBatchMessage` per tick using server-to-client notification `404` (`GameRpc.Methods.PARTICLE_BURSTS` in `WaywardBeyond.Networking/Rpc/GameRpc.cs:47`, `IGameClientApi` in `WaywardBeyond.Networking/Rpc/IGameClientApi.cs:64-68`). Bursts already implied by existing voxel RPCs (`VoxelEdit` break sparks and `VoxelFaceEdit` coat splatter) remain client-derived at zero extra wire cost.

## Client synchronization

`ClientParticleReplicationSystem` (`WaywardBeyond.Client/Systems/ClientParticleReplicationSystem.cs:21-261`) runs immediately after `ClientReconcileSystem` (`WaywardBeyond.Client/Injector.cs:136`).

- **Burst draining:** Discards queued burst batches, releases any managed `ChildComponent` and `ParticleEmitterMutedComponent` tracking, and resets unknown-effect log caches while `GameState` is below `GameState.Playing` (`GameState.Paused` keeps syncing active bindings and bursts). During play and pause, validates up to `MAX_INBOUND_BURSTS_PER_BATCH` (`256`) entries per batch, calls the 7-parameter `IParticleService.EmitBurst`, and logs unknown effect ids once.
- **Binding sync:** Queries `ParticleEmitterComponent` and `ParticleEmitterBindingComponent`. When `ParentEntity` names a distinct entity with a valid `TransformComponent`, attaches `ChildComponent` and copies the parent transform immediately. Applies `ParticleEmitterMutedComponent` whenever the parent is missing, non-finite, or self-referential. Cleans up managed `ChildComponent` and `ParticleEmitterMutedComponent` entries when parents clear or bindings are removed.
- **Entering-play coalesce:** During the `Loading` to `Playing` transition, `ClientReconcileSystem` (`WaywardBeyond.Client/Systems/ClientReconcileSystem.cs:73-93,381-428`) retains the newest `TransformComponent` of any non-player entity absent from later snapshots so stationary world-space replicated emitters keep their spawn transform across multi-snapshot bursts.

## Source of truth

- Wire schemas: `WaywardBeyond.Data/Nsd/{components,network}.nsd`, `WaywardBeyond.Data/Components/ParticleEmitterBindingComponent.cs`
- Codec, registry, and RPC: `WaywardBeyond.Networking/Codecs/ParticleEmitterCodec.cs`, `WaywardBeyond.Networking/NetworkingComposition.cs:50-51`, `WaywardBeyond.Networking/Rpc/{GameRpc,IGameClientApi}.cs`
- Server service and flush: `WaywardBeyond.Server/Particles/{ServerParticleBurst,IServerParticleService,ServerParticleService}.cs`, `WaywardBeyond.Server/Systems/ServerParticleFlushSystem.cs`
- Client sync and queues: `WaywardBeyond.Client/Networking/{ClientInbound,ClientHandlers}.cs`, `WaywardBeyond.Client/Systems/ClientParticleReplicationSystem.cs`, `WaywardBeyond.Client/Injector.cs:136`

## Tests that pin this

- `WaywardBeyond.Tests/ParticleReplicationTests.cs`
- `WaywardBeyond.Tests/PaintSprayerEffectsTests.cs`
- `WaywardBeyond.Tests/RpcProtocolTests.cs`
