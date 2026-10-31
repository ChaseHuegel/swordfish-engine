using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Systems;

/// <summary>
///     Renders bounding box debug lines for the nearest voxel chunk and voxel object extents.
/// </summary>
public sealed class VoxelDebugGizmoSystem : IEntitySystem, IDisposable
{
    private static readonly Vector4 _chunkColor = new(0f, 0.85f, 1f, 1f);
    private static readonly Vector4 _extentColor = new(1f, 0.85f, 0f, 1f);

    private readonly DebugSettings _debugSettings;
    private readonly IRenderContext _renderContext;
    private readonly Line[] _chunkLines;
    private readonly Line[] _extentLines;
    private bool _visible;

    public VoxelDebugGizmoSystem(
        DebugSettings debugSettings,
        IRenderContext renderContext,
        ILineRenderer lineRenderer
    ) {
        _debugSettings = debugSettings;
        _renderContext = renderContext;

        _chunkLines = new Line[12];
        _extentLines = new Line[12];
        for (var i = 0; i < 12; i++)
        {
            _chunkLines[i] = lineRenderer.CreateLine(alwaysOnTop: false);
            _chunkLines[i].Color = Vector4.Zero;
        }

        for (var i = 0; i < 12; i++)
        {
            _extentLines[i] = lineRenderer.CreateLine(alwaysOnTop: false);
            _extentLines[i].Color = Vector4.Zero;
        }
    }

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        if (!_debugSettings.OverlayVisible.Get() || !_debugSettings.VoxelGizmosVisible.Get())
        {
            if (_visible)
            {
                HideLines();
            }

            return;
        }

        CameraEntity camera = _renderContext.MainCamera.Get();
        Vector3 cameraPosition = camera.Transform.Position;

        var action = new QueryAction(cameraPosition);
        store.Query<VoxelComponent, TransformComponent, QueryAction>(delta, ref action);

        if (!action.Found)
        {
            if (_visible)
            {
                HideLines();
            }

            return;
        }

        _visible = true;

        float s = action.ChunkSize;
        Short3 chunk = action.BestChunkCoords;
        Vector3 chunkMin = new(chunk.X * s - 0.5f, chunk.Y * s - 0.5f, chunk.Z * s - 0.5f);
        Vector3 chunkMax = new((chunk.X + 1) * s - 0.5f, (chunk.Y + 1) * s - 0.5f, (chunk.Z + 1) * s - 0.5f);
        ApplyBoxLines(_chunkLines, chunkMin, chunkMax, action.BestTransform, _chunkColor);

        Short3 minC = action.MinChunk;
        Short3 maxC = action.MaxChunk;
        Vector3 extMin = new(minC.X * s - 0.5f, minC.Y * s - 0.5f, minC.Z * s - 0.5f);
        Vector3 extMax = new((maxC.X + 1) * s - 0.5f, (maxC.Y + 1) * s - 0.5f, (maxC.Z + 1) * s - 0.5f);
        ApplyBoxLines(_extentLines, extMin, extMax, action.BestTransform, _extentColor);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        for (var i = 0; i < 12; i++)
        {
            _chunkLines[i].Dispose();
            _extentLines[i].Dispose();
        }
    }

    private void HideLines()
    {
        _visible = false;
        for (var i = 0; i < 12; i++)
        {
            _chunkLines[i].Color = Vector4.Zero;
            _extentLines[i].Color = Vector4.Zero;
        }
    }

    private static void ApplyBoxLines(
        Line[] lines,
        Vector3 min,
        Vector3 max,
        in TransformComponent transform,
        Vector4 color
    )
    {
        Vector3 c0 = LocalToWorld(new Vector3(min.X, min.Y, min.Z), in transform);
        Vector3 c1 = LocalToWorld(new Vector3(max.X, min.Y, min.Z), in transform);
        Vector3 c2 = LocalToWorld(new Vector3(max.X, min.Y, max.Z), in transform);
        Vector3 c3 = LocalToWorld(new Vector3(min.X, min.Y, max.Z), in transform);
        Vector3 c4 = LocalToWorld(new Vector3(min.X, max.Y, min.Z), in transform);
        Vector3 c5 = LocalToWorld(new Vector3(max.X, max.Y, min.Z), in transform);
        Vector3 c6 = LocalToWorld(new Vector3(max.X, max.Y, max.Z), in transform);
        Vector3 c7 = LocalToWorld(new Vector3(min.X, max.Y, max.Z), in transform);

        // Bottom quad
        SetLine(lines[0], c0, c1, color);
        SetLine(lines[1], c1, c2, color);
        SetLine(lines[2], c2, c3, color);
        SetLine(lines[3], c3, c0, color);

        // Top quad
        SetLine(lines[4], c4, c5, color);
        SetLine(lines[5], c5, c6, color);
        SetLine(lines[6], c6, c7, color);
        SetLine(lines[7], c7, c4, color);

        // Vertical pillars
        SetLine(lines[8], c0, c4, color);
        SetLine(lines[9], c1, c5, color);
        SetLine(lines[10], c2, c6, color);
        SetLine(lines[11], c3, c7, color);
    }

    private static void SetLine(Line line, Vector3 start, Vector3 end, Vector4 color)
    {
        line.Start = start;
        line.End = end;
        line.Color = color;
    }

    private static Vector3 LocalToWorld(Vector3 local, in TransformComponent transform)
    {
        return transform.Position + Vector3.Transform(local * transform.Scale, transform.Orientation);
    }

    private struct QueryAction(Vector3 cameraPosition) : IForEach<VoxelComponent, TransformComponent>
    {
        public bool Found;
        public float BestDistSq = float.MaxValue;
        public Short3 BestChunkCoords;
        public byte ChunkSize;
        public TransformComponent BestTransform;
        public Short3 MinChunk;
        public Short3 MaxChunk;

        public void Execute(float delta, DataStore store, int entity, in VoxelComponent voxel, in TransformComponent transform)
        {
            VoxelObject voxelObject = voxel.VoxelObject;
            if (voxelObject.ChunkCount == 0)
            {
                return;
            }

            byte size = voxelObject.ChunkSize;
            float s = size;
            float halfOffset = s * 0.5f - 0.5f;

            short minX = short.MaxValue;
            short minY = short.MaxValue;
            short minZ = short.MaxValue;
            short maxX = short.MinValue;
            short maxY = short.MinValue;
            short maxZ = short.MinValue;

            Short3 closestChunk = default;
            float closestDistSq = float.MaxValue;

            foreach (ChunkData chunk in voxelObject)
            {
                Short3 coords = chunk.Coords;
                if (coords.X < minX) minX = coords.X;
                if (coords.Y < minY) minY = coords.Y;
                if (coords.Z < minZ) minZ = coords.Z;
                if (coords.X > maxX) maxX = coords.X;
                if (coords.Y > maxY) maxY = coords.Y;
                if (coords.Z > maxZ) maxZ = coords.Z;

                Vector3 localCenter = new(coords.X * s + halfOffset, coords.Y * s + halfOffset, coords.Z * s + halfOffset);
                Vector3 worldCenter = transform.Position + Vector3.Transform(localCenter * transform.Scale, transform.Orientation);
                float distSq = Vector3.DistanceSquared(cameraPosition, worldCenter);

                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestChunk = coords;
                }
            }

            if (closestDistSq < BestDistSq)
            {
                BestDistSq = closestDistSq;
                BestChunkCoords = closestChunk;
                ChunkSize = size;
                BestTransform = transform;
                MinChunk = new Short3(minX, minY, minZ);
                MaxChunk = new Short3(maxX, maxY, maxZ);
                Found = true;
            }
        }
    }
}
