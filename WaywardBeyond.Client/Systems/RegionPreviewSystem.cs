using System;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Items;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Renders the selection wireframe box, paste wireframe box, and translucent 3D holographic ghost
/// mesh from <see cref="RegionToolState"/> when the local player holds the Blueprint Projector.
/// </summary>
internal sealed class RegionPreviewSystem(
    in RegionToolState toolState,
    in RegionPreviewMeshBuilder meshBuilder,
    in VoxelMaterials materials,
    in ILineRenderer lineRenderer,
    in PlayerData playerData,
    in IBrickDatabase brickDatabase
) : IEntitySystem, IDisposable
{
    private const float SELECTION_OUTSET = 0.04f;
    private const float PASTE_OUTSET = 0.025f;

    private static readonly Vector4 _selectionValidColor = new(0.30f, 0.90f, 0.95f, 0.95f);
    private static readonly Vector4 _selectionPartialColor = new(0.95f, 0.72f, 0.28f, 0.95f);
    private static readonly Vector4 _selectionInvalidColor = new(0.95f, 0.25f, 0.25f, 0.95f);
    private static readonly Vector4 _pasteValidColor = new(0.30f, 0.95f, 0.90f, 0.90f);
    private static readonly Vector4 _pasteInvalidColor = new(0.95f, 0.25f, 0.25f, 0.90f);

    private readonly RegionToolState _toolState = toolState;
    private readonly RegionPreviewMeshBuilder _meshBuilder = meshBuilder;
    private readonly VoxelMaterials _materials = materials;
    private readonly ILineRenderer _lineRenderer = lineRenderer;
    private readonly PlayerData _playerData = playerData;
    private readonly IBrickDatabase _brickDatabase = brickDatabase;

    private readonly Line[] _selectionLines = CreateBoxLines(lineRenderer);
    private readonly Line[] _pasteLines = CreateBoxLines(lineRenderer);
    private readonly Vector3[] _corners = new Vector3[8];

    private int _entity = Entity.Null;
    private Uuid _uuid = Uuid.Null;
    private MeshKey? _activeKey;
    private MeshRenderer? _activeRenderer;
    private bool _hasGhostTransform;
    private Vector3 _lastGhostPosition;
    private Quaternion _lastGhostOrientation;
    private Vector3 _lastGhostScale;
    private bool _disposed;

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        if (!WaywardBeyond.IsInGame())
        {
            HideBox(_selectionLines);
            HideBox(_pasteLines);
            ReleaseEntity(store);
            return;
        }

        if (!IsHoldingProjector(store))
        {
            HideBox(_selectionLines);
            HideBox(_pasteLines);
            HideGhost(store);
            return;
        }

        UpdateSelectionWireframe(store);
        UpdatePastePreview(store);
    }

    private void UpdateSelectionWireframe(DataStore store)
    {
        if (!_toolState.TryGetSelectionAnchors(
                out Uuid entityUuid,
                out bool hasA,
                out Int3 _,
                out bool hasB,
                out Int3 _,
                out Int3 min,
                out Int3 max,
                out bool withinLimits)
            || !store.TryGet(entityUuid, out int targetEntity)
            || !store.TryGet(targetEntity, out TransformComponent targetTransform))
        {
            HideBox(_selectionLines);
            return;
        }

        Vector4 color = !withinLimits
            ? _selectionInvalidColor
            : (hasA && hasB ? _selectionValidColor : _selectionPartialColor);

        var localMin = new Vector3(min.X - 0.5f - SELECTION_OUTSET, min.Y - 0.5f - SELECTION_OUTSET, min.Z - 0.5f - SELECTION_OUTSET);
        var localMax = new Vector3(max.X + 0.5f + SELECTION_OUTSET, max.Y + 0.5f + SELECTION_OUTSET, max.Z + 0.5f + SELECTION_OUTSET);
        DrawTransformedBox(_selectionLines, localMin, localMax, targetTransform.Position, targetTransform.Orientation, targetTransform.Scale, color);
    }

    private void UpdatePastePreview(DataStore store)
    {
        if (_toolState.Mode != RegionToolMode.Paste
            || !_toolState.TryGetPreviewPlacement(
                out bool valid,
                out _,
                out _,
                out _,
                out Int3 size,
                out Vector3 worldPosition,
                out Quaternion worldOrientation,
                out Vector3 localOffset)
            || !_toolState.TryGetTransformedClipboard(_brickDatabase, out VoxelRegionData region, out int revision, out Orientation rotation, out MirrorAxes mirror))
        {
            HideBox(_pasteLines);
            HideGhost(store);
            return;
        }

        Vector3 worldScale = _toolState.PreviewWorldScale;
        var localMin = new Vector3(
            localOffset.X - 0.5f - PASTE_OUTSET,
            localOffset.Y - 0.5f - PASTE_OUTSET,
            localOffset.Z - 0.5f - PASTE_OUTSET);
        var localMax = new Vector3(
            localOffset.X + size.X - 0.5f + PASTE_OUTSET,
            localOffset.Y + size.Y - 0.5f + PASTE_OUTSET,
            localOffset.Z + size.Z - 0.5f + PASTE_OUTSET);
        DrawTransformedBox(_pasteLines, localMin, localMax, worldPosition, worldOrientation, worldScale, valid ? _pasteValidColor : _pasteInvalidColor);

        EnsureEntity(store);
        var key = new MeshKey(revision, rotation, mirror, valid);
        if (_activeKey != key || _activeRenderer == null || _activeRenderer.IsDisposed)
        {
            if (!TrySetGhostRenderer(store, in region, valid, in key))
            {
                HideGhost(store);
                return;
            }
        }

        Vector3 ghostPosition = worldPosition + Vector3.Transform(localOffset * worldScale, worldOrientation);
        if (!_hasGhostTransform
            || _lastGhostPosition != ghostPosition
            || _lastGhostOrientation != worldOrientation
            || _lastGhostScale != worldScale)
        {
            store.AddOrUpdate(_entity, new TransformComponent(ghostPosition, worldOrientation, worldScale));
            _lastGhostPosition = ghostPosition;
            _lastGhostOrientation = worldOrientation;
            _lastGhostScale = worldScale;
            _hasGhostTransform = true;
        }
    }

    private bool IsHoldingProjector(DataStore store)
    {
        Result<ItemSlot> mainHand = _playerData.GetMainHand(store);
        return mainHand.Success && mainHand.Value.Item.Tool?.Type == ToolType.Blueprint;
    }

    private void EnsureEntity(DataStore store)
    {
        if (_entity != Entity.Null)
        {
            if (store.GetUuid(_entity) == _uuid)
            {
                return;
            }

            ResetEntity();
        }

        _entity = store.Alloc();
        _uuid = store.GetUuid(_entity);
        store.AddOrUpdate(_entity, new IdentifierComponent(name: null, tag: "game"));
        store.AddOrUpdate(_entity, new TransformComponent());
    }

    private bool TrySetGhostRenderer(DataStore store, in VoxelRegionData region, bool valid, in MeshKey key)
    {
        Result<Mesh> meshResult = _meshBuilder.Build(in region, key.Revision, key.Rotation, key.Mirror, valid);
        if (!meshResult.Success)
        {
            return false;
        }

        var renderer = new MeshRenderer(meshResult.Value, _materials.Ghost, _materials.GhostOptions);
        _activeKey = key;
        _activeRenderer = renderer;
        store.AddOrUpdate(_entity, new MeshRendererComponent(renderer));
        return true;
    }

    private void HideGhost(DataStore store)
    {
        if (_entity == Entity.Null)
        {
            _activeKey = null;
            _activeRenderer = null;
            return;
        }

        if (store.GetUuid(_entity) != _uuid)
        {
            ResetEntity();
            return;
        }

        if (_activeRenderer != null)
        {
            store.Remove(typeof(MeshRendererComponent), _entity);
            _activeRenderer = null;
        }

        _activeKey = null;
    }

    private void ReleaseEntity(DataStore store)
    {
        _toolState.HidePreview();
        HideGhost(store);

        if (_entity != Entity.Null && store.GetUuid(_entity) == _uuid)
        {
            store.Free(_entity);
        }

        ResetEntity();
    }

    private void ResetEntity()
    {
        _entity = Entity.Null;
        _uuid = Uuid.Null;
        _activeKey = null;
        _activeRenderer = null;
        _hasGhostTransform = false;
    }

    private void DrawTransformedBox(
        Line[] lines,
        in Vector3 min,
        in Vector3 max,
        in Vector3 worldPosition,
        in Quaternion worldOrientation,
        in Vector3 worldScale,
        in Vector4 color
    )
    {
        _corners[0] = worldPosition + Vector3.Transform(new Vector3(min.X, min.Y, min.Z) * worldScale, worldOrientation);
        _corners[1] = worldPosition + Vector3.Transform(new Vector3(max.X, min.Y, min.Z) * worldScale, worldOrientation);
        _corners[2] = worldPosition + Vector3.Transform(new Vector3(max.X, min.Y, max.Z) * worldScale, worldOrientation);
        _corners[3] = worldPosition + Vector3.Transform(new Vector3(min.X, min.Y, max.Z) * worldScale, worldOrientation);
        _corners[4] = worldPosition + Vector3.Transform(new Vector3(min.X, max.Y, min.Z) * worldScale, worldOrientation);
        _corners[5] = worldPosition + Vector3.Transform(new Vector3(max.X, max.Y, min.Z) * worldScale, worldOrientation);
        _corners[6] = worldPosition + Vector3.Transform(new Vector3(max.X, max.Y, max.Z) * worldScale, worldOrientation);
        _corners[7] = worldPosition + Vector3.Transform(new Vector3(min.X, max.Y, max.Z) * worldScale, worldOrientation);

        // Bottom loop (0-1-2-3)
        SetLine(lines[0], _corners[0], _corners[1], color);
        SetLine(lines[1], _corners[1], _corners[2], color);
        SetLine(lines[2], _corners[2], _corners[3], color);
        SetLine(lines[3], _corners[3], _corners[0], color);
        // Top loop (4-5-6-7)
        SetLine(lines[4], _corners[4], _corners[5], color);
        SetLine(lines[5], _corners[5], _corners[6], color);
        SetLine(lines[6], _corners[6], _corners[7], color);
        SetLine(lines[7], _corners[7], _corners[4], color);
        // Verticals
        SetLine(lines[8], _corners[0], _corners[4], color);
        SetLine(lines[9], _corners[1], _corners[5], color);
        SetLine(lines[10], _corners[2], _corners[6], color);
        SetLine(lines[11], _corners[3], _corners[7], color);
    }

    private static void SetLine(Line line, in Vector3 start, in Vector3 end, in Vector4 color)
    {
        line.Start = start;
        line.End = end;
        line.Color = color;
    }

    private static void HideBox(Line[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i].Color = Vector4.Zero;
        }
    }

    private static Line[] CreateBoxLines(ILineRenderer lineRenderer)
    {
        var lines = new Line[12];
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = lineRenderer.CreateLine(Vector3.Zero, Vector3.Zero, Vector4.Zero);
        }

        return lines;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_activeRenderer != null)
        {
            _activeRenderer.Dispose();
            _activeRenderer.Mesh.Dispose();
            _activeRenderer = null;
        }

        for (var i = 0; i < _selectionLines.Length; i++)
        {
            _lineRenderer.DeleteLine(_selectionLines[i]);
            _pasteLines[i].Color = Vector4.Zero;
            _lineRenderer.DeleteLine(_pasteLines[i]);
        }

        ResetEntity();
    }

    private readonly record struct MeshKey(int Revision, Orientation Rotation, MirrorAxes Mirror, bool Valid);
}
