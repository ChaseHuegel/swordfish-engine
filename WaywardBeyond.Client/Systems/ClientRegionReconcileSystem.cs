using System;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Applies authoritative multi-cell <see cref="VoxelRegionEditPart"/> and newly spawned
/// <see cref="VoxelEntitySpawnPart"/> streams emitted by the server onto the
/// client's presentation store, recalculating lighting in one pass and dirtying chunks for rebuild.
/// </summary>
internal sealed class ClientRegionReconcileSystem(
    ClientInbound inbound,
    IBrickRegistry brickRegistry,
    VoxelObjectBuilder voxelObjectBuilder,
    VoxelEntityBuilder voxelEntityBuilder,
    ILogger<ClientRegionReconcileSystem> logger,
    IPaintRegistry? paintRegistry = null
) : IEntitySystem
{
    private readonly FragmentAssembler _editAssembler = new();
    private readonly FragmentAssembler _spawnAssembler = new();
    private readonly HashSet<Short3> _dirtyChunks = [];

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        if (WaywardBeyond.GameState < GameState.Playing)
        {
            _editAssembler.Reset();
            _spawnAssembler.Reset();
            return;
        }

        DrainRegionEdits(store);
        DrainEntitySpawns(store);
    }

    private void DrainRegionEdits(DataStore store)
    {
        while (inbound.VoxelRegionEdits.TryDequeue(out VoxelRegionEditPart part))
        {
            if (WaywardBeyond.GameState < GameState.Playing)
            {
                return;
            }

            try
            {
                FragmentState state = _editAssembler.Append(
                    part.BatchId,
                    (int)part.PartIndex,
                    (int)part.PartCount,
                    part.Payload ?? [],
                    out byte[] payload);

                if (state == FragmentState.Rejected)
                {
                    logger.LogWarning("Dropping an invalid region edit fragment.");
                    continue;
                }

                if (state != FragmentState.Complete)
                {
                    continue;
                }

                VoxelRegionEditMessage message = VoxelRegionEditMessage.Deserialize(payload);
                ApplyRegionEdit(store, in message);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Dropping a malformed VoxelRegionEditMessage.");
            }
        }
    }

    private void DrainEntitySpawns(DataStore store)
    {
        while (inbound.VoxelEntitySpawns.TryDequeue(out VoxelEntitySpawnPart part))
        {
            if (WaywardBeyond.GameState < GameState.Playing)
            {
                return;
            }

            try
            {
                FragmentState state = _spawnAssembler.Append(
                    part.BatchId,
                    (int)part.PartIndex,
                    (int)part.PartCount,
                    part.Payload ?? [],
                    out byte[] payload);

                if (state == FragmentState.Rejected)
                {
                    logger.LogWarning("Dropping an invalid entity spawn fragment.");
                    continue;
                }

                if (state != FragmentState.Complete)
                {
                    continue;
                }

                VoxelEntityData wireData = VoxelEntityData.Deserialize(payload);
                ApplyEntitySpawn(store, in wireData);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Dropping a malformed VoxelEntityData spawn.");
            }
        }
    }

    private void ApplyRegionEdit(DataStore store, in VoxelRegionEditMessage message)
    {
        if (!store.TryGet(Uuid.FromValue(message.EntityUuid), out int entity) ||
            !store.TryGet(entity, out VoxelComponent voxelComponent))
        {
            return;
        }

        if (!RegionLimits.IsValidSize(message.SizeX, message.SizeY, message.SizeZ, maxEdge: 0, maxVolume: 0))
        {
            logger.LogWarning("Dropping VoxelRegionEditMessage with invalid dimensions {x}x{y}x{z}.", message.SizeX, message.SizeY, message.SizeZ);
            return;
        }

        VoxelRegionData encoded = message.ToRegion();
        VoxelRegionData decoded = VoxelCodec.Decode(in encoded, brickRegistry, paintRegistry);

        _dirtyChunks.Clear();
        VoxelObject voxelObject = voxelComponent.VoxelObject;
        VoxelRegionApplier.ApplyTo(
            voxelObject,
            in decoded,
            new Int3(message.OriginX, message.OriginY, message.OriginZ),
            message.OverwriteAir,
            _dirtyChunks);

        VoxelChunkRegistry registry = voxelComponent.Registry;
        registry.DepthState.XY.Clear();
        registry.DepthState.XZ.Clear();
        registry.DepthState.ZY.Clear();
        voxelObjectBuilder.InitializeLighting(voxelObject, registry.DepthState);
        registry.DepthStateInitialized = true;

        foreach (Short3 chunkOffset in _dirtyChunks)
        {
            registry.DirtyChunks.Add(chunkOffset);
            registry.GeometryDirtyChunks.Add(chunkOffset);
            registry.LightDirtyChunks.Remove(chunkOffset);
            registry.EntityDirtyChunks.Add(chunkOffset);
            if (voxelObject.TryGetChunk(chunkOffset, out ChunkData chunk) && !chunk.Palette.Only(0))
            {
                registry.KnownChunks.Add(chunkOffset);
            }
            else
            {
                registry.KnownChunks.Remove(chunkOffset);
            }
        }

        // Sunlight columns along XY/XZ/ZY and 15-voxel light propagation can only affect chunks
        // sharing at least two padded chunk axes with the edited region.
        const long lightRadius = 15;
        int chunkShift = BitOperations.TrailingZeroCount(voxelObject.ChunkSize);
        long minChunkX = ((long)message.OriginX - lightRadius) >> chunkShift;
        long maxChunkX = ((long)message.OriginX + message.SizeX - 1 + lightRadius) >> chunkShift;
        long minChunkY = ((long)message.OriginY - lightRadius) >> chunkShift;
        long maxChunkY = ((long)message.OriginY + message.SizeY - 1 + lightRadius) >> chunkShift;
        long minChunkZ = ((long)message.OriginZ - lightRadius) >> chunkShift;
        long maxChunkZ = ((long)message.OriginZ + message.SizeZ - 1 + lightRadius) >> chunkShift;

        foreach (Short3 known in registry.KnownChunks)
        {
            bool inX = known.X >= minChunkX && known.X <= maxChunkX;
            bool inY = known.Y >= minChunkY && known.Y <= maxChunkY;
            bool inZ = known.Z >= minChunkZ && known.Z <= maxChunkZ;
            if (!((inX && inY) || (inX && inZ) || (inY && inZ)))
            {
                continue;
            }

            registry.DirtyChunks.Add(known);
            if (!registry.GeometryDirtyChunks.Contains(known))
            {
                registry.LightDirtyChunks.Add(known);
            }
        }

        store.MarkDirty<VoxelComponent>(entity);
    }

    private void ApplyEntitySpawn(DataStore store, in VoxelEntityData wireData)
    {
        Result<VoxelEntityData> decodeResult = VoxelCodec.Decode(in wireData, brickRegistry, paintRegistry);
        if (!decodeResult.Success)
        {
            logger.LogWarning("Dropped spawn of voxel entity {uuid}: {message}", wireData.Uuid, decodeResult.Message);
            return;
        }

        VoxelEntityData decoded = decodeResult.Value;
        ChunkInfo[] chunks = decoded.Chunks ?? [];
        byte chunkSize = chunks.Length > 0 ? chunks[0].Chunk.Size : (byte)0;
        if (chunkSize <= 0 || (chunkSize & (chunkSize - 1)) != 0)
        {
            chunkSize = VoxelObject.DEFAULT_CHUNK_EDGE;
        }

        var voxelObject = new VoxelObject(chunkSize, chunks);
        voxelEntityBuilder.CreateOrAttach(
            store,
            Uuid.FromValue(decoded.Uuid),
            voxelObject,
            new Vector3((float)decoded.X, (float)decoded.Y, (float)decoded.Z),
            new Quaternion(decoded.OrientationX, decoded.OrientationY, decoded.OrientationZ, decoded.OrientationW),
            new Vector3(decoded.ScaleX, decoded.ScaleY, decoded.ScaleZ));
    }
}
