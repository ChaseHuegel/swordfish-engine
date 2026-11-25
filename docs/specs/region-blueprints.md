# Region Editing and Blueprint Library

One subject: cut, copy, paste, dynamic structure spawning, and client-local blueprint files.

## Data model and transforms

Regions share the same chunk and palette model as persisted voxel structures. `RegionLimits` (`WaywardBeyond.Voxels/RegionLimits.cs:4`) enforces a 64-voxel maximum edge, a 65,536-voxel maximum volume, a 64-character blueprint name cap, a 4 MiB serialized payload cap, overflow-safe paste bounds, and `short` chunk-offset range checks.

- `VoxelRegionData` (`WaywardBeyond.Data/Nsd/blueprints.nsd:6`) stores dimensions, sparse `ChunkInfo[]` chunks, `BrickPalette`, and `PaintPalette`.
- `VoxelRegionExtractor.Extract` (`WaywardBeyond.Voxels/VoxelRegionExtractor.cs:10`) copies bricks, orientations, and per-face paint overrides into normalized `[0..size-1]` bounds.
- `VoxelRegionTransform.Transform` (`WaywardBeyond.Voxels/VoxelRegionTransform.cs:10`) mirrors first via `MirrorAxes` (`WaywardBeyond.Voxels/MirrorAxes.cs:7`) and then rotates by 90-degree `Orientation` steps, remapping cell coordinates, brick orientations, and face overrides.
- `VoxelRegionApplier.ApplyTo` (`WaywardBeyond.Voxels/VoxelRegionApplier.cs:11`) writes cells into a target `VoxelObject` with optional `overwriteAir` and collects dirty chunk coordinates.
- `VoxelRegionApplier.ToCenteredVoxelObject` (`WaywardBeyond.Voxels/VoxelRegionApplier.cs:150`) centers a region around local origin `(0, 0, 0)` for standalone structure spawning.
- `VoxelCodec.Encode` and `VoxelCodec.Decode` (`WaywardBeyond.Voxels/VoxelCodec.cs`) convert live registry IDs to portable string palettes on export and map known IDs back on import.

## Blueprint storage

Blueprints live on the client as portable `.wbp` binary nsd files.

- `StorageConfig.BlueprintRoot` (`WaywardBeyond.Config/StorageConfig.cs:18`) defaults to `blueprints/`. `StoragePaths.GetBlueprintsDirectory` (`WaywardBeyond.Data/StoragePaths.cs:23`) resolves it relative to `AppPaths.DataRoot`.
- `IBlueprintStorage` (`WaywardBeyond.Data/Blueprints/IBlueprintStorage.cs:7`) defines `List`, `Load`, `Save`, `Rename`, and `Delete` operations returning `Result` or `Result<T>`.
- `FileBlueprintStorage` (`WaywardBeyond.Data/Blueprints/FileBlueprintStorage.cs:12`) writes version-guarded `BlueprintData` (`WaywardBeyond.Data/Nsd/blueprints.nsd:16`) records via temp-file replace, enforces region size and 4 MiB file limits on save and load, caches summaries by file size and timestamp, assigns collision-safe slug IDs, and rejects path traversal names.
- `BlueprintData`, `PasteRegionRequest`, and `VoxelRegionEditMessage` inline `SizeX`, `SizeY`, `SizeZ`, `Chunks`, `BrickPalette`, and `PaintPalette` at the message root so large multi-chunk regions avoid Needlefish's 65,535-byte uint16 nested sub-message limit.
- `BlueprintDataExtensions.ToRegion` (`WaywardBeyond.Data/Blueprints/BlueprintDataExtensions.cs:7`) and `RegionMessageExtensions.ToRegion` (`WaywardBeyond.Data/Networking/RegionMessageExtensions.cs:7,19`) convert flat messages into `VoxelRegionData`.

## Wire protocol and server authority

`GameRpc.PROTOCOL_VERSION` (`WaywardBeyond.Networking/Rpc/GameRpc.cs:7`) is `3`.

| Method | ID | Direction | Kind | Notes |
|---|---|---|---|---|
| `CutRegion` | `61` | client → server | call | `CutRegionRequest` → `RegionOpResponse` |
| `PasteRegionPart` | `62` | client → server | call | `RegionUploadPart` → `RegionOpResponse` |
| `VoxelRegionEdit` | `352` | server → client | notification | `VoxelRegionEditPart` fragment |
| `VoxelEntitySpawn` | `353` | server → client | notification | `VoxelEntitySpawnPart` fragment |

- `PayloadFragmenter` (`WaywardBeyond.Networking/Rpc/PayloadFragmenter.cs`) splits serialized `PasteRegionRequest`, `VoxelRegionEditMessage`, and `VoxelEntityData` payloads over 60,000 bytes into ordered parts.
- `ServerRegionSystem` (`WaywardBeyond.Server/Systems/ServerRegionSystem.cs:32`) drains peer calls on the server tick and enforces `GameMode.Creative` (defaulting missing `GameModeComponent` to `GameMode.Adventure`) and permission nodes (`GamePermissions.REGION_CUT`, `GamePermissions.REGION_PASTE`, `GamePermissions.REGION_SPAWN` in `WaywardBeyond.Server/Permissions/GamePermissions.cs:16-22`). Paste upload parts validate Creative mode, permission, and the 4 MiB cap on `ServerPeer.RegionUploadAssembler` (`WaywardBeyond.Server/ServerPeer.cs:109`) before buffering.
- `RegionPlacementResolver` (`WaywardBeyond.Gameplay/Interactions/RegionPlacementResolver.cs:13`) and `CharacterDefaults` (`WaywardBeyond.Gameplay/CharacterDefaults.cs:23-26`) resolve flush or shifted-embed origins, free-space poses aligned to the player's up orientation, 32-voxel tool reach, 128-voxel paste max reach, and world collider intersections.
- Server paste decodes incoming palettes without auto-registering unknown bricks (`IBrickRegistry.Id`), treats missing or empty palettes as empty maps instead of raw IDs, counts missing entries in `RegionOpResponse.SkippedUnknownBricks` (`notification.region.pasteSkippedUnknown`), and rejects zero-brick pastes when spawning a new structure or pasting without `OverwriteAir`.
- When a cut or empty-cell overwrite paste clears every solid brick on an entity, `ServerRegionSystem` requests entity despawn and frees the server entity. Otherwise it rebuilds dirty colliders in one batch via `VoxelColliderBuilder.RebuildDirtyChunks` and broadcasts fragmented edits or spawns.

## Client projector tool, preview, and UI

- `wb:projector` (`WaywardBeyond.Items/assets/items/projector.toml`) declares `ToolType.Blueprint` (`WaywardBeyond.Items/ToolType.cs`) and ships in `PlayerInitialInventory.StarterInventory()` (`WaywardBeyond.Server/Systems/PlayerInitialInventory.cs`).
- `RegionToolState` (`WaywardBeyond.Client/Systems/RegionToolState.cs:17`) stores the active entity, anchors, clipboard region, rotation, mirror mask, `OverwriteAir`, free-space distance, cached transformed region, and preview pose. Disconnect clears selection and preview while keeping the clipboard.
- `RegionToolService` (`WaywardBeyond.Client/Systems/RegionToolService.cs:40`) reads LMB/RMB clicks, `C`, `X`, `V`, `T`, `Shift+T`, `F`, `Shift+F`, `G`, `Shift` embed, and `Shift+Scroll` distance adjustments on the ECS thread and invokes `RegionClient` (`WaywardBeyond.Client/Networking/RegionClient.cs:14`). Session generation guards discard stale RPC callbacks and queued saves on disconnect. Validation errors, mode toggles, and transient tool actions emit `NotificationType.Action`; blueprint save/rename/delete events and skipped-brick warnings emit `NotificationType.Toast`.
- `RegionPreviewMeshBuilder` (`WaywardBeyond.Client/Voxels/Building/RegionPreviewMeshBuilder.cs:18`), `voxelGhost.glsl` (`WaywardBeyond.Client/assets/shaders/voxelGhost.glsl`), `VoxelMaterials.Ghost`, and `RegionPreviewSystem` (`WaywardBeyond.Client/Systems/RegionPreviewSystem.cs:22`) render selection/paste wireframes and the translucent 3D holographic ghost mesh positioned via `TransformComponent`.
- `ClientRegionReconcileSystem` (`WaywardBeyond.Client/Systems/ClientRegionReconcileSystem.cs:24`) reassembles `VoxelRegionEditPart` and `VoxelEntitySpawnPart` queues, resets `DepthState`, recalculates lighting in one pass, marks affected geometry chunks, scopes light-only dirty chunks to intersecting orthogonal columns and light radius, prunes emptied chunks from `KnownChunks`, and calls `VoxelEntityBuilder.CreateOrAttach` so snapshot-preallocated entities keep their `TransformComponent`.
- `RegionToolRadial` (`WaywardBeyond.Client/UI/Layers/RegionToolRadial.cs:19`) binds hold-`R` radial actions (dispatching on release rather than hover) and `IActionIndicator` icons.
- `BlueprintLibraryLayer` (`WaywardBeyond.Client/UI/Layers/Blueprints/BlueprintLibraryLayer.cs:20`) and passive `BlueprintLibraryView` (`WaywardBeyond.Client/UI/Layers/Blueprints/BlueprintLibraryView.cs:10`) expose the `B` modal to save, search, paste, inline rename, and two-step delete blueprints.

## Tests

- `WaywardBeyond.Voxels.Tests/VoxelRegionTests.cs` — extraction, 90-degree rotation, mirroring, face override remapping, apply with air overwrite, and codec round-trips.
- `WaywardBeyond.Tests/Blueprints/FileBlueprintStorageTests.cs` — `.wbp` persistence, >64 KiB multi-chunk round-trips, slug collisions, rename, delete, and traversal rejection.
- `WaywardBeyond.Tests/StoragePathsTests.cs` — default and configured `BlueprintRoot` path resolution.
- `WaywardBeyond.Tests/ServerRegionSystemTests.cs` — Creative gate, permission denial, unknown brick skip counts, reach and overlap validation, cut-to-empty despawn, and multi-part broadcast.
- `WaywardBeyond.Tests/ProjectorToolTests.cs` — `wb:projector` asset definition, starter inventory entry, and localization keys.
- `WaywardBeyond.Tests/ClientRegionReconcileTests.cs` — multi-part edit and spawn stream reassembly, lighting reset, chunk dirtying, and `CreateOrAttach` transform preservation.
- `WaywardBeyond.Tests/RegionToolTests.cs` — anchor limits, transform caching, disconnect preservation, blueprint loading, and radial hover-vs-release dispatch.
- `WaywardBeyond.Tests/BlueprintLibraryViewTests.cs` — headless Reef modal rendering, button callbacks, rename/delete workflows, search filtering, and `B` shortcut gating while open.
