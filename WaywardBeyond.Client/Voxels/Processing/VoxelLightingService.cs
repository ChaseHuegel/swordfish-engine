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

                if (registry.DepthState.IsAmbientSource(nx, ny, nz))
                {
                    if (neighbor.GetLightLevel() < 15)
                    {
                        voxelObject.SetLight(nx, ny, nz, 15);
                        registry.MarkVoxelModified(nx, ny, nz, voxelObject.ChunkSize);
                    }

                    _addQueue.Enqueue((nx, ny, nz, 15));
                }
            }
        }
        else
        {
            int maxNeighborLight = 0;
            var adjacentToSky = false;

            for (var i = 0; i < _directions.Length; i++)
            {
                (int dx, int dy, int dz) = _directions[i];
                int nx = x + dx;
                int ny = y + dy;
                int nz = z + dz;
                Short3 neighborChunkOffset = voxelObject.ToChunkOffset(nx, ny, nz);
                if (!voxelObject.TryGetChunk(neighborChunkOffset, out _))
                {
                    adjacentToSky = true;
                    continue;
                }

                Voxel neighbor = voxelObject.Get(nx, ny, nz);
                maxNeighborLight = Math.Max(maxNeighborLight, neighbor.GetLightLevel());
            }

            int finalLight = adjacentToSky ? 15 : Math.Max(0, maxNeighborLight - 1);

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

                bool isSource = false;
                if (neighborLight >= light)
                {
                    Result<Brick> brickResult = _brickDatabase.Get(neighbor.ID);
                    if (brickResult.Success && brickResult.Value.LightSource && brickResult.Value.Brightness >= neighborLight)
                    {
                        isSource = true;
                    }
                    else if (neighborLight == 15 && registry.DepthState.IsAmbientSource(nx, ny, nz))
                    {
                        isSource = true;
                    }
                }

                if (neighborLight != 0 && (neighborLight < light || (neighborLight == light && !isSource)))
                {
                    voxelObject.SetLight(nx, ny, nz, 0);
                    registry.MarkVoxelModified(nx, ny, nz, voxelObject.ChunkSize);
                    _removeQueue.Enqueue((nx, ny, nz, neighborLight));
                }
                else if (neighborLight >= light && isSource)
                {
                    _addQueue.Enqueue((nx, ny, nz, neighborLight));
                }
            }
        }
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

                if (shapeLight.LightLevel >= nextLight)
                {
                    continue;
                }

                if (_brickDatabase.IsCuller(neighbor, shapeLight.Shape))
                {
                    continue;
                }

                voxelObject.SetLight(nx, ny, nz, nextLight);
                registry.MarkVoxelModified(nx, ny, nz, voxelObject.ChunkSize);
                _addQueue.Enqueue((nx, ny, nz, nextLight));
            }
        }
    }
}
