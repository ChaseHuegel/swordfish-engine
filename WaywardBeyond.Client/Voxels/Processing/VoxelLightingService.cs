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
        Result<Brick> oldBrickResult = _brickDatabase.Get(original.ID);
        Result<Brick> newBrickResult = _brickDatabase.Get(updated.ID);

        bool oldIsLight = oldBrickResult.Success && oldBrickResult.Value.LightSource;
        bool newIsLight = newBrickResult.Success && newBrickResult.Value.LightSource;

        int oldBrightness = oldIsLight ? oldBrickResult.Value.Brightness : 0;
        int newBrightness = newIsLight ? newBrickResult.Value.Brightness : 0;

        bool oldIsCuller = _brickDatabase.IsCuller(original, original.GetShapeLight().Shape);
        bool newIsCuller = _brickDatabase.IsCuller(updated, updated.GetShapeLight().Shape);

        registry.MarkVoxelModified(x, y, z, voxelObject.ChunkSize);

        if (newIsLight)
        {
            updated.ShapeLight = new ShapeLight(updated.GetShapeLight().Shape, newBrightness);
            voxelObject.Set(x, y, z, updated);
            _addQueue.Enqueue((x, y, z, newBrightness));
        }
        else if (oldIsLight)
        {
            updated.ShapeLight = new ShapeLight(updated.GetShapeLight().Shape, 0);
            voxelObject.Set(x, y, z, updated);
            _removeQueue.Enqueue((x, y, z, oldBrightness));
        }
        else if (newIsCuller && !oldIsCuller)
        {
            int oldLight = original.GetLightLevel();
            updated.ShapeLight = new ShapeLight(updated.GetShapeLight().Shape, 0);
            voxelObject.Set(x, y, z, updated);
            if (oldLight > 0)
            {
                _removeQueue.Enqueue((x, y, z, oldLight));
            }
        }
        else if (oldIsCuller && !newIsCuller)
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

            if (maxNeighborLight > 1)
            {
                int nextLight = maxNeighborLight - 1;
                updated.ShapeLight = new ShapeLight(updated.GetShapeLight().Shape, nextLight);
                voxelObject.Set(x, y, z, updated);
                _addQueue.Enqueue((x, y, z, nextLight));
            }
        }

        DrainQueues(voxelObject, registry);
    }

    private void DrainQueues(VoxelObject voxelObject, VoxelChunkRegistry registry)
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

                if (neighborLight != 0 && neighborLight < light)
                {
                    voxelObject.SetLight(nx, ny, nz, 0);
                    registry.MarkVoxelModified(nx, ny, nz, voxelObject.ChunkSize);
                    _removeQueue.Enqueue((nx, ny, nz, neighborLight));
                }
                else if (neighborLight >= light)
                {
                    _addQueue.Enqueue((nx, ny, nz, neighborLight));
                }
            }
        }

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
