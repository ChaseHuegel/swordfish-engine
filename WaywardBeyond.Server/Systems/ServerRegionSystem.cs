using System;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging;
using NsdRpc;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Items;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server-authoritative handler for region cut, copy/blueprint paste, standalone structure
/// spawning, and blueprint placeable items. Drains inbound <see cref="ServerPeer.CutRegionCalls"/>,
/// <see cref="ServerPeer.PasteRegionParts"/>, and <see cref="ServerPeer.PlaceBlueprintItemCalls"/> on
/// the server world tick, validates permissions and placement bounds, sanitizes payloads against
/// server brick/paint registries, mutates the authoritative world, and broadcasts fragmented updates
/// to all peers in the world.
/// </summary>
public sealed class ServerRegionSystem(
    ServerPeers peers,
    SessionManager sessions,
    IUserPermissionService permissions,
    IServerWorldPhysics physics,
    NetworkReplicationSystem replication,
    IBrickRegistry brickRegistry,
    IBrickDatabase brickDatabase,
    ILogger<ServerRegionSystem> logger,
    IPaintRegistry? paintRegistry = null,
    IPaintDatabase? paintDatabase = null,
    GameplayConfig? gameplayConfig = null,
    IItemDatabase? itemDatabase = null,
    IBundledBlueprintDatabase? bundledBlueprints = null,
    ServerBrickEntityService? brickEntities = null
) : IServerWorldSystem
{
    private const float MIN_QUATERNION_LEN_SQ = 1e-4f;
    private const byte VALID_ROTATION_MASK = 63;
    private const byte VALID_MIRROR_MASK = (byte)(MirrorAxes.X | MirrorAxes.Y | MirrorAxes.Z);

    private readonly HashSet<Short3> _dirtyChunks = [];
    private uint _nextBatchId;

    private int MaxRegionEdge => gameplayConfig?.Regions.MaxEdge.Get() ?? RegionLimits.MAX_REGION_EDGE;
    private int MaxRegionVolume => gameplayConfig?.Regions.MaxVolume.Get() ?? RegionLimits.MAX_REGION_VOLUME;
    private int MaxRegionPayloadBytes => gameplayConfig?.Regions.MaxPayloadBytes.Get() ?? RegionLimits.MAX_PAYLOAD_BYTES;

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        foreach (Uuid clientId in sessions.Clients)
        {
            if (!peers.TryGet(clientId, out ServerPeer peer))
            {
                continue;
            }

            while (peer.CutRegionCalls.TryDequeue(out ServerPeer.CutRegionCall cutCall))
            {
                HandleCut(store, peer, in cutCall);
            }

            while (peer.PasteRegionParts.TryDequeue(out ServerPeer.PasteRegionPartCall pasteCall))
            {
                HandlePastePart(store, peer, in pasteCall);
            }

            while (peer.PlaceBlueprintItemCalls.TryDequeue(out ServerPeer.PlaceBlueprintItemCall placeCall))
            {
                HandlePlaceBlueprintItem(store, peer, in placeCall);
            }
        }
    }

    private void HandleCut(DataStore store, ServerPeer peer, in ServerPeer.CutRegionCall call)
    {
        if (call.CancellationToken.IsCancellationRequested)
        {
            call.Completion.TrySetResult(RpcResult<RegionOpResponse>.Failure(
                GameRpcErrors.INVALID_STATE, "the cut region request was cancelled"));
            return;
        }

        if (!TryValidateActor(store, peer, GamePermissions.REGION_CUT, out TransformComponent playerTransform, out RpcResult<RegionOpResponse> gateError))
        {
            call.Completion.TrySetResult(gateError);
            return;
        }

        CutRegionRequest request = call.Request;
        var min = new Int3(request.MinX, request.MinY, request.MinZ);
        var max = new Int3(request.MaxX, request.MaxY, request.MaxZ);
        if (!RegionLimits.IsValidBounds(min, max, MaxRegionEdge, MaxRegionVolume))
        {
            call.Completion.TrySetResult(FailOp("notification.region.invalidBounds"));
            return;
        }

        if (!TryGetTargetStructure(store, request.TargetEntity, out int targetEntity, out VoxelWorldComponent world, out TransformComponent targetTransform))
        {
            call.Completion.TrySetResult(FailOp("notification.region.invalidTarget"));
            return;
        }

        if (!RegionLimits.IsWithinChunkRange(min, max, world.VoxelObject.ChunkSize))
        {
            call.Completion.TrySetResult(FailOp("notification.region.invalidBounds"));
            return;
        }

        if (!RegionPlacementResolver.IsBoxWithinReach(min, max, playerTransform.Position, in targetTransform, CharacterDefaults.REGION_PASTE_MAX_REACH))
        {
            call.Completion.TrySetResult(FailOp("notification.region.outOfReach"));
            return;
        }

        _dirtyChunks.Clear();
        int cleared = VoxelRegionApplier.Clear(world.VoxelObject, min, max, _dirtyChunks);
        if (cleared > 0)
        {
            Uuid targetUuid = store.GetUuid(targetEntity);
            if (!VoxelRegionApplier.HasSolidVoxels(world.VoxelObject))
            {
                brickEntities?.OnStructureRemoved(store, targetUuid);
                replication.RequestDespawn(targetUuid.ToValue());
                store.Free(targetEntity);
            }
            else
            {
                brickEntities?.OnRegionCleared(store, targetUuid, min, max);
                store.AddOrUpdate(targetEntity, new ColliderComponent(
                    VoxelColliderBuilder.RebuildDirtyChunks(in world, _dirtyChunks, brickDatabase)));

                var message = new VoxelRegionEditMessage
                {
                    EntityUuid = request.TargetEntity,
                    OriginX = min.X,
                    OriginY = min.Y,
                    OriginZ = min.Z,
                    OverwriteAir = true,
                    SizeX = max.X - min.X + 1,
                    SizeY = max.Y - min.Y + 1,
                    SizeZ = max.Z - min.Z + 1,
                    Chunks = [],
                    BrickPalette = null,
                    PaintPalette = null,
                };
                BroadcastRegionEdit(store, in message);
            }
        }

        call.Completion.TrySetResult(RpcResult<RegionOpResponse>.Success(new RegionOpResponse
        {
            Success = true,
            Completed = true,
            AffectedBricks = cleared,
        }));
    }

    private void HandlePastePart(DataStore store, ServerPeer peer, in ServerPeer.PasteRegionPartCall call)
    {
        if (call.CancellationToken.IsCancellationRequested)
        {
            call.Completion.TrySetResult(RpcResult<RegionOpResponse>.Failure(
                GameRpcErrors.INVALID_STATE, "the paste region request was cancelled"));
            return;
        }

        if (!TryValidatePasteUploadActor(store, peer, out RpcResult<RegionOpResponse> uploadGateError))
        {
            peer.RegionUploadAssembler.Reset();
            call.Completion.TrySetResult(uploadGateError);
            return;
        }

        RegionUploadPart part = call.Part;
        int maxPayloadBytes = MaxRegionPayloadBytes;
        if (maxPayloadBytes > 0 && part.PartCount > (uint)PayloadFragmenter.PartCount(maxPayloadBytes))
        {
            peer.RegionUploadAssembler.Reset();
            call.Completion.TrySetResult(FailOp("notification.region.invalidBounds"));
            return;
        }

        FragmentState state = peer.RegionUploadAssembler.Append(
            part.BatchId,
            (int)part.PartIndex,
            (int)part.PartCount,
            part.Payload ?? [],
            out byte[] payload);

        if (state == FragmentState.Rejected)
        {
            call.Completion.TrySetResult(RpcResult<RegionOpResponse>.Failure(
                GameRpcErrors.INVALID_STATE, "invalid region upload fragment"));
            return;
        }

        if (state == FragmentState.Incomplete)
        {
            call.Completion.TrySetResult(RpcResult<RegionOpResponse>.Success(new RegionOpResponse
            {
                Success = true,
                Completed = false,
            }));
            return;
        }

        if (!RegionLimits.IsValidPayloadSize(payload.Length, maxPayloadBytes))
        {
            call.Completion.TrySetResult(FailOp("notification.region.invalidBounds"));
            return;
        }

        PasteRegionRequest request;
        try
        {
            request = PasteRegionRequest.Deserialize(payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rejected malformed PasteRegionRequest from client {clientId}.", peer.GameId);
            call.Completion.TrySetResult(RpcResult<RegionOpResponse>.Failure(
                GameRpcErrors.INVALID_STATE, "malformed paste region request"));
            return;
        }

        string requiredPermission = request.TargetKind == PasteTargetKind.NewStructure
            ? GamePermissions.REGION_SPAWN
            : GamePermissions.REGION_PASTE;

        if (!TryValidateActor(store, peer, requiredPermission, out TransformComponent playerTransform, out RpcResult<RegionOpResponse> gateError))
        {
            call.Completion.TrySetResult(gateError);
            return;
        }

        if (!RegionLimits.IsValidSize(request.SizeX, request.SizeY, request.SizeZ, MaxRegionEdge, MaxRegionVolume))
        {
            call.Completion.TrySetResult(FailOp("notification.region.invalidBounds"));
            return;
        }

        VoxelRegionData incomingRegion = request.ToRegion();
        if (!TrySanitizeRegion(in incomingRegion, out VoxelRegionData sanitized, out int validBrickCount, out int skippedUnknownBricks))
        {
            call.Completion.TrySetResult(FailOp("notification.region.invalidBounds"));
            return;
        }

        if (request.TargetKind == PasteTargetKind.NewStructure)
        {
            call.Completion.TrySetResult(ExecuteNewStructurePaste(
                store,
                in playerTransform,
                new Vector3(request.SpawnX, request.SpawnY, request.SpawnZ),
                new Quaternion(request.SpawnOrientationX, request.SpawnOrientationY, request.SpawnOrientationZ, request.SpawnOrientationW),
                in sanitized,
                validBrickCount,
                skippedUnknownBricks));
            return;
        }

        call.Completion.TrySetResult(ExecuteIntoStructurePaste(
            store,
            in playerTransform,
            request.TargetEntity,
            new Int3(request.OriginX, request.OriginY, request.OriginZ),
            request.OverwriteAir,
            disallowTargetOverlap: false,
            in sanitized,
            validBrickCount,
            skippedUnknownBricks));
    }

    private void HandlePlaceBlueprintItem(DataStore store, ServerPeer peer, in ServerPeer.PlaceBlueprintItemCall call)
    {
        if (call.CancellationToken.IsCancellationRequested)
        {
            call.Completion.TrySetResult(RpcResult<RegionOpResponse>.Failure(
                GameRpcErrors.INVALID_STATE, "the blueprint placement request was cancelled"));
            return;
        }

        if (!sessions.TryGetEntity(peer.GameId, out int playerEntity) ||
            !store.TryGet(playerEntity, out TransformComponent playerTransform))
        {
            call.Completion.TrySetResult(RpcResult<RegionOpResponse>.Failure(
                GameRpcErrors.NOT_JOINED, "the peer has no active session entity"));
            return;
        }

        if (store.TryGet(playerEntity, out StatisticsComponent playerStats) && playerStats.IsDead)
        {
            call.Completion.TrySetResult(FailOp("notification.region.invalidTarget"));
            return;
        }

        if (itemDatabase == null || bundledBlueprints == null)
        {
            call.Completion.TrySetResult(FailOp("notification.blueprint.loadFailed"));
            return;
        }

        if (!store.TryGet(playerEntity, out EquipmentComponent equipment)
            || !store.TryGet(playerEntity, out InventoryComponent inventory))
        {
            call.Completion.TrySetResult(FailOp("notification.region.invalidTarget"));
            return;
        }

        var hotbarSlot = new SlotId(SlotGroups.HOTBAR, equipment.ActiveInventorySlot);
        ItemData held = inventory.Get(in hotbarSlot);
        if (held.IsEmpty())
        {
            call.Completion.TrySetResult(FailOp("notification.region.invalidTarget"));
            return;
        }

        Result<BlueprintData> blueprintResult = ItemPlaceables.ResolveBlueprint(itemDatabase, bundledBlueprints, held.ID);
        if (!blueprintResult.Success)
        {
            call.Completion.TrySetResult(FailOp("notification.blueprint.loadFailed"));
            return;
        }

        PlaceBlueprintItemRequest request = call.Request;
        if ((request.TargetKind != PasteTargetKind.IntoStructure && request.TargetKind != PasteTargetKind.NewStructure)
            || (request.Rotation & ~VALID_ROTATION_MASK) != 0
            || (request.Mirror & ~VALID_MIRROR_MASK) != 0)
        {
            call.Completion.TrySetResult(FailOp("notification.region.invalidTarget"));
            return;
        }

        VoxelRegionData rawRegion = blueprintResult.Value.ToRegion();
        if (!RegionLimits.IsValidSize(rawRegion.SizeX, rawRegion.SizeY, rawRegion.SizeZ, MaxRegionEdge, MaxRegionVolume) ||
            !TrySanitizeRegion(in rawRegion, out VoxelRegionData sanitized, out int validBrickCount, out int skippedUnknownBricks))
        {
            call.Completion.TrySetResult(FailOp("notification.region.invalidBounds"));
            return;
        }

        VoxelRegionData transformed = VoxelRegionTransform.Transform(
            in sanitized,
            new Orientation(request.Rotation),
            (MirrorAxes)request.Mirror,
            brickDatabase);
        if (!RegionLimits.IsValidSize(transformed.SizeX, transformed.SizeY, transformed.SizeZ, MaxRegionEdge, MaxRegionVolume))
        {
            call.Completion.TrySetResult(FailOp("notification.region.invalidBounds", skippedUnknownBricks));
            return;
        }

        RpcResult<RegionOpResponse> result = request.TargetKind == PasteTargetKind.NewStructure
            ? ExecuteNewStructurePaste(
                store,
                in playerTransform,
                new Vector3(request.SpawnX, request.SpawnY, request.SpawnZ),
                new Quaternion(request.SpawnOrientationX, request.SpawnOrientationY, request.SpawnOrientationZ, request.SpawnOrientationW),
                in transformed,
                validBrickCount,
                skippedUnknownBricks)
            : ExecuteIntoStructurePaste(
                store,
                in playerTransform,
                request.TargetEntity,
                new Int3(request.OriginX, request.OriginY, request.OriginZ),
                overwriteAir: false,
                disallowTargetOverlap: true,
                in transformed,
                validBrickCount,
                skippedUnknownBricks);

        if (result.IsSuccess && result.Value.Success && result.Value.AffectedBricks > 0)
        {
            GameMode mode = store.TryGet(playerEntity, out GameModeComponent gameMode) ? gameMode.Mode : GameMode.Adventure;
            if (mode != GameMode.Creative)
            {
                InventoryComponent updated = inventory.Clone();
                updated.Remove(in hotbarSlot, 1);
                store.AddOrUpdate(playerEntity, updated);
            }
        }

        call.Completion.TrySetResult(result);
    }

    private RpcResult<RegionOpResponse> ExecuteIntoStructurePaste(
        DataStore store,
        in TransformComponent playerTransform,
        ulong targetEntityUuid,
        Int3 origin,
        bool overwriteAir,
        bool disallowTargetOverlap,
        in VoxelRegionData sanitized,
        int validBrickCount,
        int skippedUnknownBricks
    )
    {
        if (validBrickCount == 0 && !overwriteAir)
        {
            return FailOp("notification.region.empty", skippedUnknownBricks);
        }

        if (!TryGetTargetStructure(store, targetEntityUuid, out int targetEntity, out VoxelWorldComponent world, out TransformComponent targetTransform))
        {
            return FailOp("notification.region.invalidTarget", skippedUnknownBricks);
        }

        if (!RegionLimits.TryComputePasteMax(origin, sanitized.SizeX, sanitized.SizeY, sanitized.SizeZ, out Int3 max, MaxRegionEdge, MaxRegionVolume)
            || !RegionLimits.IsWithinChunkRange(origin, max, world.VoxelObject.ChunkSize))
        {
            return FailOp("notification.region.invalidBounds", skippedUnknownBricks);
        }

        if (!RegionPlacementResolver.IsBoxWithinReach(origin, max, playerTransform.Position, in targetTransform, CharacterDefaults.REGION_PASTE_MAX_REACH))
        {
            return FailOp("notification.region.outOfReach", skippedUnknownBricks);
        }

        if (disallowTargetOverlap && validBrickCount > 0 &&
            VoxelRegionApplier.OverlapsSolidVoxels(world.VoxelObject, in sanitized, origin))
        {
            return FailOp("notification.placement.occupied", skippedUnknownBricks);
        }

        if (validBrickCount > 0 &&
            RegionPlacementResolver.IntersectsWorldColliders(physics, targetEntity, in targetTransform, origin, in sanitized, brickDatabase))
        {
            return FailOp("notification.placement.occupied", skippedUnknownBricks);
        }

        _dirtyChunks.Clear();
        int applied = VoxelRegionApplier.ApplyTo(world.VoxelObject, in sanitized, origin, overwriteAir, _dirtyChunks);

        if (!VoxelRegionApplier.HasSolidVoxels(world.VoxelObject))
        {
            Uuid targetUuid = store.GetUuid(targetEntity);
            brickEntities?.OnStructureRemoved(store, targetUuid);
            replication.RequestDespawn(targetUuid.ToValue());
            store.Free(targetEntity);
        }
        else if (_dirtyChunks.Count > 0 || applied > 0)
        {
            brickEntities?.SyncRegion(
                store,
                store.GetUuid(targetEntity),
                world.VoxelObject,
                origin,
                sanitized.SizeX,
                sanitized.SizeY,
                sanitized.SizeZ);
            store.AddOrUpdate(targetEntity, new ColliderComponent(
                VoxelColliderBuilder.RebuildDirtyChunks(in world, _dirtyChunks, brickDatabase)));

            VoxelRegionData encoded = VoxelCodec.Encode(in sanitized, brickRegistry, paintRegistry);
            var message = new VoxelRegionEditMessage
            {
                EntityUuid = targetEntityUuid,
                OriginX = origin.X,
                OriginY = origin.Y,
                OriginZ = origin.Z,
                OverwriteAir = overwriteAir,
                SizeX = encoded.SizeX,
                SizeY = encoded.SizeY,
                SizeZ = encoded.SizeZ,
                Chunks = encoded.Chunks,
                BrickPalette = encoded.BrickPalette,
                PaintPalette = encoded.PaintPalette,
            };
            BroadcastRegionEdit(store, in message);
        }

        return RpcResult<RegionOpResponse>.Success(new RegionOpResponse
        {
            Success = true,
            Completed = true,
            AffectedBricks = applied,
            SkippedUnknownBricks = skippedUnknownBricks,
        });
    }

    private RpcResult<RegionOpResponse> ExecuteNewStructurePaste(
        DataStore store,
        in TransformComponent playerTransform,
        Vector3 spawnPosition,
        Quaternion rawOrientation,
        in VoxelRegionData sanitized,
        int validBrickCount,
        int skippedUnknownBricks
    )
    {
        if (validBrickCount == 0)
        {
            return FailOp("notification.region.empty", skippedUnknownBricks);
        }

        if (!IsFinite(spawnPosition) || !IsFinite(rawOrientation) || rawOrientation.LengthSquared() < MIN_QUATERNION_LEN_SQ)
        {
            return FailOp("notification.region.invalidTarget", skippedUnknownBricks);
        }

        Quaternion spawnOrientation = Quaternion.Normalize(rawOrientation);
        if (!RegionPlacementResolver.IsPointWithinReach(spawnPosition, playerTransform.Position, CharacterDefaults.REGION_PASTE_MAX_REACH))
        {
            return FailOp("notification.region.outOfReach", skippedUnknownBricks);
        }

        VoxelObject centered = VoxelRegionApplier.ToCenteredVoxelObject(in sanitized, out Int3 centerOffset);
        var spawnTransform = new TransformComponent(spawnPosition, spawnOrientation, Vector3.One);

        if (RegionPlacementResolver.IntersectsWorldColliders(physics, ignoredEntity: -1, in spawnTransform, centerOffset, in sanitized, brickDatabase))
        {
            return FailOp("notification.placement.occupied", skippedUnknownBricks);
        }

        Uuid newUuid;
        do
        {
            newUuid = Uuid.NewUuid();
        }
        while (newUuid == Uuid.Null || store.TryGet(newUuid, out _));

        var rawEntityData = new VoxelEntityData(
            newUuid.ToValue(),
            spawnPosition.X,
            spawnPosition.Y,
            spawnPosition.Z,
            spawnOrientation.X,
            spawnOrientation.Y,
            spawnOrientation.Z,
            spawnOrientation.W,
            1f,
            1f,
            1f,
            centered.GetChunkInfos(),
            _BrickPalette: null,
            _PaintPalette: null);

        Entity spawned = VoxelWorldEntityFactory.CreateAuthority(store, in rawEntityData, brickDatabase);
        if (spawned.Ptr == Entity.Null)
        {
            return FailOp("notification.region.empty", skippedUnknownBricks);
        }

        if (brickEntities != null && store.TryGet(spawned.Ptr, out VoxelWorldComponent spawnedWorld))
        {
            brickEntities.SyncStructure(store, newUuid, spawnedWorld.VoxelObject);
        }

        Result<VoxelEntityData> encodedSpawn = VoxelCodec.Encode(in rawEntityData, brickRegistry, paintRegistry);
        if (!encodedSpawn.Success)
        {
            logger.LogWarning("Failed to encode spawned voxel entity {uuid}: {message}", newUuid, encodedSpawn.Message);
        }
        else
        {
            BroadcastEntitySpawn(store, in encodedSpawn.Value);
        }

        return RpcResult<RegionOpResponse>.Success(new RegionOpResponse
        {
            Success = true,
            Completed = true,
            SpawnedEntityUuid = newUuid.ToValue(),
            AffectedBricks = validBrickCount,
            SkippedUnknownBricks = skippedUnknownBricks,
        });
    }

    private bool TryValidatePasteUploadActor(
        DataStore store,
        ServerPeer peer,
        out RpcResult<RegionOpResponse> error
    )
    {
        if (!sessions.TryGetEntity(peer.GameId, out int playerEntity) ||
            !store.TryGet(playerEntity, out TransformComponent _))
        {
            error = RpcResult<RegionOpResponse>.Failure(GameRpcErrors.NOT_JOINED, "the peer has no active session entity");
            return false;
        }

        if (store.TryGet(playerEntity, out StatisticsComponent playerStats) && playerStats.IsDead)
        {
            error = FailOp("notification.region.invalidTarget");
            return false;
        }

        GameMode mode = store.TryGet(playerEntity, out GameModeComponent gameMode) ? gameMode.Mode : GameMode.Adventure;
        if (mode != GameMode.Creative)
        {
            error = FailOp("notification.region.creativeOnly");
            return false;
        }

        if (!permissions.HasPermission(peer.GameId, GamePermissions.REGION_PASTE)
            && !permissions.HasPermission(peer.GameId, GamePermissions.REGION_SPAWN))
        {
            logger.LogInformation(
                "Denied region paste upload for client {clientId}: lacks \"{paste}\" and \"{spawn}\".",
                peer.GameId, GamePermissions.REGION_PASTE, GamePermissions.REGION_SPAWN);
            error = RpcResult<RegionOpResponse>.Failure(
                GameRpcErrors.PERMISSION_DENIED, "region operation is not permitted");
            return false;
        }

        error = default;
        return true;
    }

    private bool TryValidateActor(
        DataStore store,
        ServerPeer peer,
        string requiredPermission,
        out TransformComponent playerTransform,
        out RpcResult<RegionOpResponse> error
    )
    {
        if (!sessions.TryGetEntity(peer.GameId, out int playerEntity) ||
            !store.TryGet(playerEntity, out playerTransform))
        {
            playerTransform = default;
            error = RpcResult<RegionOpResponse>.Failure(GameRpcErrors.NOT_JOINED, "the peer has no active session entity");
            return false;
        }

        if (store.TryGet(playerEntity, out StatisticsComponent playerStats) && playerStats.IsDead)
        {
            error = FailOp("notification.region.invalidTarget");
            return false;
        }

        GameMode mode = store.TryGet(playerEntity, out GameModeComponent gameMode) ? gameMode.Mode : GameMode.Adventure;
        if (mode != GameMode.Creative)
        {
            error = FailOp("notification.region.creativeOnly");
            return false;
        }

        if (!permissions.HasPermission(peer.GameId, requiredPermission))
        {
            logger.LogInformation(
                "Denied region operation for client {clientId}: lacks permission \"{permission}\".",
                peer.GameId, requiredPermission);
            error = RpcResult<RegionOpResponse>.Failure(
                GameRpcErrors.PERMISSION_DENIED, "region operation is not permitted");
            return false;
        }

        error = default;
        return true;
    }

    private static bool TryGetTargetStructure(
        DataStore store,
        ulong targetEntityUuid,
        out int targetEntity,
        out VoxelWorldComponent world,
        out TransformComponent transform
    )
    {
        if (!store.TryGet(Uuid.FromValue(targetEntityUuid), out targetEntity) ||
            !store.TryGet(targetEntity, out world) ||
            !store.TryGet(targetEntity, out transform))
        {
            targetEntity = Entity.Null;
            world = default;
            transform = default;
            return false;
        }

        return true;
    }

    private bool TrySanitizeRegion(
        in VoxelRegionData source,
        out VoxelRegionData sanitized,
        out int validBrickCount,
        out int skippedUnknownBricks
    )
    {
        validBrickCount = 0;
        skippedUnknownBricks = 0;

        ChunkInfo[]? chunks = source.Chunks;
        if (chunks == null || chunks.Length == 0)
        {
            sanitized = new VoxelRegionData(source.SizeX, source.SizeY, source.SizeZ, [], null, null);
            return true;
        }

        byte chunkSize = chunks[0].Chunk.Size;
        if (chunkSize == 0 || chunkSize > 64 || (chunkSize & (chunkSize - 1)) != 0)
        {
            sanitized = default;
            return false;
        }

        long maxChunksX = ((long)source.SizeX + chunkSize - 1) / chunkSize;
        long maxChunksY = ((long)source.SizeY + chunkSize - 1) / chunkSize;
        long maxChunksZ = ((long)source.SizeZ + chunkSize - 1) / chunkSize;
        long maxChunks = Math.Min(ushort.MaxValue, maxChunksX * maxChunksY * maxChunksZ);
        if (chunks.Length > maxChunks)
        {
            sanitized = default;
            return false;
        }

        int voxelsPerChunk = chunkSize * chunkSize * chunkSize;
        for (var i = 0; i < chunks.Length; i++)
        {
            Chunk chunk = chunks[i].Chunk;
            if (chunk.Size != chunkSize || chunk.Voxels == null || chunk.Voxels.Length != voxelsPerChunk)
            {
                sanitized = default;
                return false;
            }
        }

        var sourceObject = new VoxelObject(chunkSize, chunks);
        ushort[] brickMap = BuildBrickLookup(source.BrickPalette);
        ushort[] paintMap = BuildPaintLookup(source.PaintPalette);

        var destObject = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        int shift = BitOperations.TrailingZeroCount(chunkSize);
        int shift2 = shift * 2;
        int mask = chunkSize - 1;

        foreach (ChunkData chunk in sourceObject)
        {
            if (chunk.Palette.Only(0))
            {
                continue;
            }

            int baseX = chunk.Coords.X * chunkSize;
            int baseY = chunk.Coords.Y * chunkSize;
            int baseZ = chunk.Coords.Z * chunkSize;
            Voxel[] voxels = chunk.Data.Voxels;

            for (var i = 0; i < voxels.Length; i++)
            {
                Voxel raw = voxels[i];
                if (raw.ID == 0)
                {
                    continue;
                }

                int sx = baseX + (i & mask);
                int sy = baseY + ((i >> shift) & mask);
                int sz = baseZ + (i >> shift2);
                if ((uint)sx >= (uint)source.SizeX || (uint)sy >= (uint)source.SizeY || (uint)sz >= (uint)source.SizeZ)
                {
                    continue;
                }

                ushort serverBrickId = MapBrickId(raw.ID, brickMap);
                Result<Brick> brickResult = serverBrickId != 0
                    ? brickDatabase.Get(serverBrickId)
                    : Result<Brick>.FromFailure((string?)null);
                if (!brickResult.Success)
                {
                    skippedUnknownBricks++;
                    continue;
                }

                Brick brick = brickResult.Value;
                BrickShape rawShape = raw.GetShapeLight().Shape;
                BrickShape shape = brick.Shapeable
                    ? (rawShape is >= BrickShape.Block and <= BrickShape.Plate ? rawShape : BrickShape.Block)
                    : brick.Shape;
                Orientation orientation = brick.IsOrientable(shape) ? raw.GetOrientation() : Orientation.Identity;

                var clean = new Voxel(
                    serverBrickId,
                    new ShapeLight(shape, lightLevel: 0),
                    new OrientationFlags(orientation, hasFaceOverrides: false));
                destObject.Set(sx, sy, sz, clean);
                validBrickCount++;

                if (!raw.HasFaceOverrides() || !brick.Paintable || paintDatabase == null || paintRegistry == null)
                {
                    continue;
                }

                SanitizeFaceOverrides(chunk, i, brick, destObject, sx, sy, sz, paintMap);
            }
        }

        sanitized = new VoxelRegionData(
            source.SizeX,
            source.SizeY,
            source.SizeZ,
            destObject.GetChunkInfos(),
            _BrickPalette: null,
            _PaintPalette: null);
        return true;
    }

    private void SanitizeFaceOverrides(
        ChunkData sourceChunk,
        int sourceIndex,
        Brick brick,
        VoxelObject destObject,
        int sx,
        int sy,
        int sz,
        ushort[] paintMap
    )
    {
        for (var f = 0; f < 6; f++)
        {
            var face = (BrickFace)f;
            if (!sourceChunk.TryGetFaceOverride(sourceIndex, face, out FaceOverride rawOverride) || rawOverride.IsEmpty)
            {
                continue;
            }

            ushort textureId = SanitizePaintId(rawOverride.TexturePaintId, PaintKind.Texture, brick.Alpha, paintMap);
            ushort colorId = SanitizePaintId(rawOverride.ColorPaintId, PaintKind.Color, brick.Alpha, paintMap);
            if (textureId == 0 && colorId == 0)
            {
                continue;
            }

            destObject.SetFaceOverride(sx, sy, sz, face.ToMask(), new FaceOverride(textureId, colorId));
        }
    }

    private ushort SanitizePaintId(
        ushort rawPaintId,
        PaintKind expectedKind,
        BrickAlpha brickAlpha,
        ushort[] paintMap
    )
    {
        if (rawPaintId == 0 || paintDatabase == null)
        {
            return 0;
        }

        ushort serverPaintId = rawPaintId < paintMap.Length ? paintMap[rawPaintId] : (ushort)0;

        if (serverPaintId == 0)
        {
            return 0;
        }

        Result<Paint> paintResult = paintDatabase.Get(serverPaintId);
        if (!paintResult.Success || paintResult.Value.Kind != expectedKind)
        {
            return 0;
        }

        if (expectedKind == PaintKind.Texture && paintResult.Value.Alpha != brickAlpha)
        {
            return 0;
        }

        return serverPaintId;
    }

    private ushort[] BuildBrickLookup(string[]? palette)
    {
        if (palette == null || palette.Length == 0)
        {
            return [];
        }

        var map = new ushort[palette.Length];
        for (var i = 1; i < palette.Length; i++)
        {
            string name = palette[i];
            if (!string.IsNullOrEmpty(name))
            {
                map[i] = brickRegistry.Id(name);
            }
        }

        return map;
    }

    private ushort[] BuildPaintLookup(string[]? palette)
    {
        if (palette == null || palette.Length == 0 || paintRegistry == null)
        {
            return [];
        }

        var map = new ushort[palette.Length];
        for (var i = 1; i < palette.Length; i++)
        {
            string name = palette[i];
            if (!string.IsNullOrEmpty(name))
            {
                map[i] = paintRegistry.Id(name);
            }
        }

        return map;
    }

    private static ushort MapBrickId(ushort rawId, ushort[] brickMap)
    {
        return rawId < brickMap.Length ? brickMap[rawId] : (ushort)0;
    }

    private void BroadcastRegionEdit(DataStore store, in VoxelRegionEditMessage message)
    {
        byte[] payload = message.Serialize();
        uint batchId = ++_nextBatchId;
        int partCount = PayloadFragmenter.PartCount(payload.Length);

        foreach (Uuid clientId in sessions.Clients)
        {
            if (!peers.TryGet(clientId, out ServerPeer peer) || peer.World?.Store != store)
            {
                continue;
            }

            for (var partIndex = 0; partIndex < partCount; partIndex++)
            {
                var part = new VoxelRegionEditPart
                {
                    BatchId = batchId,
                    PartIndex = (uint)partIndex,
                    PartCount = (uint)partCount,
                    Payload = PayloadFragmenter.Slice(payload, partIndex),
                };
                if (!peer.Client.VoxelRegionEdit(part))
                {
                    peer.CloseOnRejectedSend(logger);
                    break;
                }
            }
        }
    }

    private void BroadcastEntitySpawn(DataStore store, in VoxelEntityData entityData)
    {
        ServerVoxelEntityBroadcaster.BroadcastEntitySpawn(store, sessions, peers, logger, in entityData);
    }

    private static RpcResult<RegionOpResponse> FailOp(string errorKey, int skippedUnknownBricks = 0)
    {
        return RpcResult<RegionOpResponse>.Success(new RegionOpResponse
        {
            Success = false,
            Completed = true,
            ErrorKey = errorKey,
            SkippedUnknownBricks = skippedUnknownBricks,
        });
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    private static bool IsFinite(Quaternion value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W);
    }
}
