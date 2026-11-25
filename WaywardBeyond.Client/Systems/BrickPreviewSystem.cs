using System;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Systems;

/// <summary>
///     Renders the textured brick placement preview from the published <see cref="BrickPreviewState"/>.
/// </summary>
internal sealed class BrickPreviewSystem(
    in BrickPreviewState state,
    in BrickPreviewMeshBuilder meshBuilder,
    in VoxelMaterials materials
) : IEntitySystem, IDisposable {
    private const double PULSE_SPEED = 4.0;
    private const float PULSE_MIN = 0.98f;
    private const float PULSE_RANGE = 0.02f;

    private readonly BrickPreviewState _state = state;
    private readonly BrickPreviewMeshBuilder _meshBuilder = meshBuilder;
    private readonly VoxelMaterials _materials = materials;

    private int _entity = Entity.Null;
    private Uuid _uuid = Uuid.Null;
    private PreviewKey? _activeKey;
    private MeshRenderer? _activeRenderer;
    private double _time;

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        if (!WaywardBeyond.IsInGame())
        {
            Release(store);
            return;
        }

        Brick? brick = _state.Brick;
        if (!_state.Visible || brick == null)
        {
            Hide(store);
            return;
        }

        EnsureEntity(store);

        var key = new PreviewKey(brick.ID, _state.Shape, _state.Orientation);
        if (_activeKey != key || _activeRenderer == null || _activeRenderer.IsDisposed)
        {
            if (!TrySetRenderer(store, brick, key))
            {
                Hide(store);
                return;
            }
        }

        _time += delta;
        float pulse = PULSE_MIN + PULSE_RANGE * (float)Math.Abs(Math.Sin(_time * PULSE_SPEED));
        store.AddOrUpdate(_entity, new TransformComponent(_state.Position, _state.StructureOrientation, new Vector3(pulse)));
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

    private bool TrySetRenderer(DataStore store, Brick brick, in PreviewKey key)
    {
        Result<Mesh> meshResult = _meshBuilder.Build(brick, _state.Shape, _state.Orientation);
        if (!meshResult.Success)
        {
            return false;
        }

        RenderOptions options = _materials.GetRenderOptions(brick.Alpha);
        options.CastShadows = false;
        var renderer = new MeshRenderer(meshResult.Value, _materials.GetMaterial(brick.Alpha), options);

        _activeKey = key;
        _activeRenderer = renderer;
        store.AddOrUpdate(_entity, new MeshRendererComponent(renderer));
        return true;
    }

    private void Hide(DataStore store)
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

    private void Release(DataStore store)
    {
        _state.Hide();
        Hide(store);

        if (_entity != Entity.Null && store.GetUuid(_entity) == _uuid)
        {
            store.Free(_entity);
        }

        _entity = Entity.Null;
        _uuid = Uuid.Null;
        _activeKey = null;
        _activeRenderer = null;
    }

    private void ResetEntity()
    {
        _entity = Entity.Null;
        _uuid = Uuid.Null;
        _activeKey = null;
        _activeRenderer = null;
    }

    private void DisposeActiveRenderer()
    {
        if (_activeRenderer == null)
        {
            return;
        }

        _activeRenderer.Dispose();
        _activeRenderer.Mesh.Dispose();
        _activeRenderer = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        DisposeActiveRenderer();
        _entity = Entity.Null;
        _uuid = Uuid.Null;
        _activeKey = null;
    }

    private readonly record struct PreviewKey(string BrickID, BrickShape Shape, Orientation Orientation);
}
