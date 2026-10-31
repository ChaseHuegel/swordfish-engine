using System;
using System.Collections.Generic;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Processing;

/// <summary>
/// Provides localized flood-fill lighting propagation for voxel edits.
/// </summary>
internal sealed class VoxelLightingService(IBrickDatabase brickDatabase)
{
    private readonly IBrickDatabase _brickDatabase = brickDatabase;
    private readonly Queue<(int X, int Y, int Z, int Light)> _addQueue = new();
    private readonly Queue<(int X, int Y, int Z, int Light)> _removeQueue = new();

    private static readonly (int X, int Y, int Z)[] _directions =
    [
        (-1, 0, 0),
        (1, 0, 0),
        (0, -1, 0),
        (0, 1, 0),
        (0, 0, -1),
        (0, 0, 1),
    ];

    /// <summary>Updates localized lighting for a changed voxel and marks affected chunks dirty.</summary>
    public void UpdateLighting(
        VoxelObject voxelObject,
        VoxelChunkRegistry registry,
        int x,
        int y,
        int z,
        in Voxel original,
        ref Voxel updated
    )
    {
        EnsureDepthState(voxelObject, registry);
        registry.MarkVoxelModified(x, y, z, voxelObject.ChunkSize);

        Result<Brick> oldBrickResult = _brickDatabase.Get(original.ID);
        Result<Brick> newBrickResult = _brickDatabase.Get(updated.ID);

        bool oldIsLight = oldBrickResult.Success && oldBrickResult.Value.LightSource;
        bool newIsLight = newBrickResult.Success && newBrickResult.Value.LightSource;

        int oldBrightness = oldIsLight ? oldBrickResult.Value.Brightness : 0;
        int newBrightness = newIsLight ? newBrickResult.Value.Brightness : 0;

        bool oldIsCuller = _brickDatabase.IsCuller(original, original.GetShapeLight().Shape);
        bool newIsCuller = _brickDatabase.IsCuller(updated, updated.GetShapeLight().Shape);

        // 1. Light emission removal
        if (oldIsLight && (!newIsLight || newBrightness < oldBrightness))
        {
            updated.ShapeLight = new ShapeLight(updated.GetShapeLight().Shape, 0);
            voxelObject.Set(x, y, z, updated);
            _removeQueue.Enqueue((x, y, z, oldBrightness));
        }

        // 2. Culler transitions and occlusion draining
        if (newIsCuller && !oldIsCuller)
        {
            int oldLight = original.GetLightLevel();
            if (oldLight == 0)
            {
                if (VoxelAmbient.IsAmbient(voxelObject, registry.DepthState, x, y, z))
                {
                    oldLight = 15;
                }
                else
                {
                    for (var i = 0; i < _directions.Length; i++)
                    {
                        (int dx, int dy, int dz) = _directions[i];
                        if (!voxelObject.TryGetChunk(voxelObject.ToChunkOffset(x + dx, y + dy, z + dz), out _))
                        {
                            oldLight = 15;
                            break;
                        }
                    }
                }
            }

            updated.ShapeLight = new ShapeLight(updated.GetShapeLight().Shape, 0);
            voxelObject.Set(x, y, z, updated);

            if (oldLight > 0)
            {
                _removeQueue.Enqueue((x, y, z, oldLight));
            }

            registry.DepthState.Add(x, y, z);
        }
        else if (oldIsCuller && !newIsCuller)
        {
            voxelObject.Set(x, y, z, updated);
            registry.DepthState.Remove(voxelObject, _brickDatabase, x, y, z);
        }

        if (_removeQueue.Count > 0)
        {
            DrainRemoveQueue(voxelObject, registry);
        }

        //  A write can make a new chunk resident. Seed its ambient air after the depth state update.
        SeedNewChunk(voxelObject, registry, x, y, z);

        // 3. Assign cell light and enqueue for propagation
        if (newIsLight)
        {
            updated.ShapeLight = new ShapeLight(updated.GetShapeLight().Shape, newBrightness);
            voxelObject.Set(x, y, z, updated);
            _addQueue.Enqueue((x, y, z, newBrightness));
        }
        else if (newIsCuller)
        {
            updated.ShapeLight = new ShapeLight(updated.GetShapeLight().Shape, 0);
            voxelObject.Set(x, y, z, updated);

            // When placing an opaque culler block, seed ambient light on adjacent air cells exposed to open sky
            for (var i = 0; i < _directions.Length; i++)
            {
                (int dx, int dy, int dz) = _directions[i];
                int nx = x + dx;
                int ny = y + dy;
                int nz = z + dz;

                Short3 neighborChunkOffset = voxelObject.ToChunkOffset(nx, ny, nz);
                if (!voxelObject.TryGetChunk(neighborChunkOffset, out _))
                {
                    continue;
                }

                Voxel neighbor = voxelObject.Get(nx, ny, nz);
                if (neighbor.ID != 0 && _brickDatabase.IsCuller(neighbor, neighbor.GetShapeLight().Shape))
                {
                    continue;
                }

                if (VoxelAmbient.IsAmbient(voxelObject, registry.DepthState, nx, ny, nz))
                {
                    if (neighbor.GetLightLevel() < 15)
                    {
                        voxelObject.SetLight(nx, ny, nz, 15);
                        registry.MarkVoxelModified(nx, ny, nz, voxelObject.ChunkSize, lightOnly: true);
                    }

                    _addQueue.Enqueue((nx, ny, nz, 15));
                }
            }
        }
        else
        {
            int maxNeighborLight = 0;

            for (var i = 0; i < _directions.Length; i++)
            {
                (int dx, int dy, int dz) = _directions[i];
                int nx = x + dx;
                int ny = y + dy;
                int nz = z + dz;
                Short3 neighborChunkOffset = voxelObject.ToChunkOffset(nx, ny, nz);
                if (!voxelObject.TryGetChunk(neighborChunkOffset, out _))
                {
                    continue;
                }

                Voxel neighbor = voxelObject.Get(nx, ny, nz);
                maxNeighborLight = Math.Max(maxNeighborLight, neighbor.GetLightLevel());
            }

            int finalLight = VoxelAmbient.IsAmbient(voxelObject, registry.DepthState, x, y, z)
                ? 15
                : Math.Max(0, maxNeighborLight - 1);

            updated.ShapeLight = new ShapeLight(updated.GetShapeLight().Shape, finalLight);
            voxelObject.Set(x, y, z, updated);

            if (finalLight > 0)
            {
                _addQueue.Enqueue((x, y, z, finalLight));
            }
        }

        DrainAddQueue(voxelObject, registry);
    }

    private void EnsureDepthState(VoxelObject voxelObject, VoxelChunkRegistry registry)
    {
        if (!registry.DepthStateInitialized)
        {
            registry.DepthState.Populate(voxelObject, _brickDatabase);
            registry.DepthStateInitialized = true;
        }
    }

    private void SeedNewChunk(VoxelObject voxelObject, VoxelChunkRegistry registry, int x, int y, int z)
    {
        Short3 chunkOffset = voxelObject.ToChunkOffset(x, y, z);
        if (!registry.KnownChunks.Add(chunkOffset))
        {
            return;
        }

        if (!voxelObject.TryGetChunk(chunkOffset, out ChunkData chunk) || chunk.Palette.Only(0))
        {
            return;
        }

        foreach (VoxelSample sample in chunk.GetSampler())
        {
            if (sample.Center.ID != 0 && _brickDatabase.IsCuller(sample.Center, sample.Center.GetShapeLight().Shape))
            {
                continue;
            }

            int wx = sample.Coords.X + sample.ChunkOffset.X;
            int wy = sample.Coords.Y + sample.ChunkOffset.Y;
            int wz = sample.Coords.Z + sample.ChunkOffset.Z;

            if (!VoxelAmbient.IsAmbient(voxelObject, registry.DepthState, wx, wy, wz) || sample.Center.GetLightLevel() >= 15)
            {
                continue;
            }

            ShapeLight shapeLight = sample.Center.GetShapeLight();
            sample.Center.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            registry.MarkVoxelModified(wx, wy, wz, voxelObject.ChunkSize, lightOnly: true);
            _addQueue.Enqueue((wx, wy, wz, 15));
        }
    }

    private void DrainRemoveQueue(VoxelObject voxelObject, VoxelChunkRegistry registry)
    {
        while (_removeQueue.Count > 0)
        {
            (int cx, int cy, int cz, int light) = _removeQueue.Dequeue();

            for (var i = 0; i < _directions.Length; i++)
            {
                (int dx, int dy, int dz) = _directions[i];
                int nx = cx + dx;
                int ny = cy + dy;
                int nz = cz + dz;

                Short3 neighborChunkOffset = voxelObject.ToChunkOffset(nx, ny, nz);
                if (!voxelObject.TryGetChunk(neighborChunkOffset, out _))
                {
                    continue;
                }

                Voxel neighbor = voxelObject.Get(nx, ny, nz);
                int neighborLight = neighbor.GetLightLevel();
                if (neighborLight == 0)
                {
                    continue;
                }

                if (neighborLight < light)
                {
                    voxelObject.SetLight(nx, ny, nz, 0);
                    registry.MarkVoxelModified(nx, ny, nz, voxelObject.ChunkSize, lightOnly: true);
                    _removeQueue.Enqueue((nx, ny, nz, neighborLight));
                }
                else if (neighborLight > light || light < 15)
                {
                    _addQueue.Enqueue((nx, ny, nz, neighborLight));
                }
                else
                {
                    if (IsIndependentSkySource(voxelObject, registry, nx, ny, nz, in neighbor))
                    {
                        _addQueue.Enqueue((nx, ny, nz, neighborLight));
                    }
                    else
                    {
                        voxelObject.SetLight(nx, ny, nz, 0);
                        registry.MarkVoxelModified(nx, ny, nz, voxelObject.ChunkSize, lightOnly: true);
                        _removeQueue.Enqueue((nx, ny, nz, neighborLight));
                    }
                }
            }
        }
    }

    private bool IsIndependentSkySource(
        VoxelObject voxelObject,
        VoxelChunkRegistry registry,
        int x,
        int y,
        int z,
        in Voxel voxel
    )
    {
        Result<Brick> brickResult = _brickDatabase.Get(voxel.ID);
        if (brickResult.Success && brickResult.Value.LightSource && brickResult.Value.Brightness >= 15)
        {
            return true;
        }

        if (VoxelAmbient.IsAmbient(voxelObject, registry.DepthState, x, y, z))
        {
            return true;
        }

        for (var i = 0; i < _directions.Length; i++)
        {
            (int dx, int dy, int dz) = _directions[i];
            Short3 neighborChunkOffset = voxelObject.ToChunkOffset(x + dx, y + dy, z + dz);
            if (!voxelObject.TryGetChunk(neighborChunkOffset, out _))
            {
                return true;
            }
        }

        return false;
    }

    private void DrainAddQueue(VoxelObject voxelObject, VoxelChunkRegistry registry)
    {
        while (_addQueue.Count > 0)
        {
            (int cx, int cy, int cz, int light) = _addQueue.Dequeue();
            if (light <= 1)
            {
                continue;
            }

            int nextLight = light - 1;

            for (var i = 0; i < _directions.Length; i++)
            {
                (int dx, int dy, int dz) = _directions[i];
                int nx = cx + dx;
                int ny = cy + dy;
                int nz = cz + dz;

                Short3 neighborChunkOffset = voxelObject.ToChunkOffset(nx, ny, nz);
                if (!voxelObject.TryGetChunk(neighborChunkOffset, out _))
                {
                    continue;
                }

                Voxel neighbor = voxelObject.Get(nx, ny, nz);
                ShapeLight shapeLight = neighbor.GetShapeLight();

                int targetLight = VoxelAmbient.IsAmbient(voxelObject, registry.DepthState, nx, ny, nz)
                    ? 15
                    : nextLight;

                if (shapeLight.LightLevel >= targetLight)
                {
                    continue;
                }

                if (_brickDatabase.IsCuller(neighbor, shapeLight.Shape))
                {
                    continue;
                }

                voxelObject.SetLight(nx, ny, nz, targetLight);
                registry.MarkVoxelModified(nx, ny, nz, voxelObject.ChunkSize, lightOnly: true);
                _addQueue.Enqueue((nx, ny, nz, targetLight));
            }
        }
    }
}
