using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Graphics;

namespace WaywardBeyond.Client.Systems;

/// <summary>
///     Renders a camera-facing textured plane for any entity carrying a <see cref="BillboardComponent"/>.
///     A billboard may carry one or more directional materials. With one material the same sprite renders
///     for every relative facing. With more, the materials are divided evenly across relative facing angles:
///     index 0 covers the sector centered on the entity's forward (its front, the facing-camera view), and
///     later indices step around the up axis. A single active material is rendered; it is rebuilt only when
///     the selected sector changes, because the renderer draws a mesh once per material.
/// </summary>
public sealed class BillboardSystem(IRenderContext renderContext) : IEntitySystem
{
    private readonly Dictionary<Uuid, BillboardState> _billboards = [];
    private readonly HashSet<Uuid> _seenOwners = [];
    private readonly List<Uuid> _staleOwners = [];
    private readonly Billboard _mesh = new();

    public void Tick(float delta, DataStore store)
    {
        _seenOwners.Clear();

        CameraEntity camera = renderContext.MainCamera.Get();
        BillboardAction action = new(camera, system: this);
        store.Query<BillboardComponent, TransformComponent, BillboardAction>(delta, ref action);

        //  Clean up billboards whose owner no longer has a billboard
        _staleOwners.Clear();
        foreach (Uuid owner in _billboards.Keys)
        {
            if (!_seenOwners.Contains(owner))
            {
                _staleOwners.Add(owner);
            }
        }

        foreach (Uuid owner in _staleOwners)
        {
            BillboardState billboardState = _billboards[owner];
            billboardState.Renderer.Dispose();
            store.Free(billboardState.Entity);
            _billboards.Remove(owner);
        }
    }

    private void AddOrUpdateBillboard(DataStore store, BillboardRecord record, Vector3 cameraPosition)
    {
        _seenOwners.Add(record.Owner);

        Vector3 position = record.Position + (Vector3.Transform(record.Offset, record.Orientation));
        Quaternion orientation = GetAxialLookAt(cameraPosition, position, record.Orientation);
        //  Mirror the quad's X so a laterally-facing character looks left or right from the camera.
        int facing = GetHorizontalFacing(record.Orientation, position, cameraPosition);
        var scale = new Vector3(record.Size.X * facing, record.Size.Y, 1f);
        var transform = new TransformComponent(position, orientation, scale);

        //  Number of materials is the number of directions: 1 renders every facing, otherwise the camera's
        //  relative position picks the sector. Only rebuild the renderer when the active material changes.
        Material[] directions = record.Materials;
        int activeIndex = SelectDirectionIndex(record.Orientation, position, cameraPosition, directions.Length);

        if (!_billboards.TryGetValue(record.Owner, out BillboardState? state))
        {
            //  Create a billboard entity if it doesn't exist
            int entity = store.Alloc();
            var meshRenderer = new MeshRenderer(_mesh, directions[activeIndex], new RenderOptions { DoubleFaced = true });
            store.AddOrUpdate(entity, new MeshRendererComponent(meshRenderer));

            state = new BillboardState(entity, meshRenderer, directions, activeIndex);
            _billboards[record.Owner] = state;
        }
        else if (!ReferenceEquals(state.Directions, directions) || activeIndex != state.ActiveIndex)
        {
            //  Update the billboard's renderer if its material set or selected sector changed
            state.Renderer.Dispose();
            state.Renderer = new MeshRenderer(_mesh, directions[activeIndex], new RenderOptions { DoubleFaced = true });
            state.Directions = directions;
            state.ActiveIndex = activeIndex;
            store.AddOrUpdate(state.Entity, new MeshRendererComponent(state.Renderer));
        }

        store.AddOrUpdate(state.Entity, transform);
    }

    /// <summary>
    ///     Selects the direction material index for a camera at <paramref name="cameraPosition"/> looking at
    ///     <paramref name="position"/>. Index 0 is the sector centered on the entity's forward. With one
    ///     material, always 0. Otherwise the relative facing angle (projected onto the plane orthogonal to the
    ///     local up axis) picks the sector it falls into.
    /// </summary>
    private static int SelectDirectionIndex(Quaternion entityOrientation, Vector3 position, Vector3 cameraPosition, int directionCount)
    {
        if (directionCount <= 1)
        {
            return 0;
        }

        Vector3 localUp = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, entityOrientation));

        //  Entity forward is the sector-0 reference; project it onto the up-orthogonal plane.
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, entityOrientation);
        Vector3 projectedForward = ProjectOnto(forward, localUp);

        //  Direction from the entity toward the camera, projected onto the same plane.
        Vector3 toCamera = cameraPosition - position;
        Vector3 projectedCamera = ProjectOnto(toCamera, localUp);

        if (projectedForward.LengthSquared() < 1e-6f || projectedCamera.LengthSquared() < 1e-6f)
        {
            //  Degenerate alignment with the up axis; fall back to the forward material.
            return 0;
        }

        projectedForward = Vector3.Normalize(projectedForward);
        projectedCamera = Vector3.Normalize(projectedCamera);

        //  Signed angle from forward to camera about localUp, wrapped to [0, 2*PI).
        float dot = MathF.Max(-1f, MathF.Min(1f, Vector3.Dot(projectedForward, projectedCamera)));
        float angle = MathF.Acos(dot);
        float sin = Vector3.Dot(Vector3.Cross(projectedForward, projectedCamera), localUp);
        if (sin < 0f)
        {
            angle = MathF.Tau - angle;
        }

        float sector = MathF.Tau / directionCount;
        //  Index 0 is centered on forward, so the half-sector offset lets the front material own the view.
        int index = (int)((angle + sector * 0.5f) / sector) % directionCount;
        return index;
    }

    private static Vector3 ProjectOnto(Vector3 value, Vector3 basis)
    {
        return value - Vector3.Dot(value, basis) * basis;
    }

    /// <summary>
    ///     Returns whether to mirror the billboard's X for a camera looking at <paramref name="position"/>.
    ///     Returns -1 (mirror) when the entity's forward points to the camera's left of the line of sight, +1
    ///     (no mirror) when it points right. Degenerate alignment with the up axis returns +1.
    /// </summary>
    private static int GetHorizontalFacing(Quaternion entityOrientation, Vector3 position, Vector3 cameraPosition)
    {
        Vector3 localUp = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, entityOrientation));

        //  Direction toward the camera is the billboard's face normal (same axis GetAxialLookAt builds).
        Vector3 facing = ProjectOnto(cameraPosition - position, localUp);
        Vector3 forward = ProjectOnto(Vector3.Transform(-Vector3.UnitZ, entityOrientation), localUp);

        if (facing.LengthSquared() < 1e-6f || forward.LengthSquared() < 1e-6f)
        {
            //  Aligned with the up axis; the mirror is ambiguous, so leave it unmirrored.
            return 1;
        }

        facing = Vector3.Normalize(facing);
        forward = Vector3.Normalize(forward);

        //  Billboard in-plane horizontal axis, orthogonal to both the face normal and the up axis.
        Vector3 right = Vector3.Cross(localUp, facing);
        return Vector3.Dot(forward, right) < 0f ? -1 : 1;
    }

    /// <summary>
    ///     Rotates around the entity's local up axis to face the camera, keeping local up fixed.
    /// </summary>
    private static Quaternion GetAxialLookAt(Vector3 target, Vector3 position, Quaternion orientation)
    {
        const float epsilonSq = 1e-6f;

        //  Local up drives the axis the billboard spins about.
        Vector3 localUp = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, orientation));

        //  Facing toward the target, projected onto the plane orthogonal to localUp.
        Vector3 desiredFacing = target - position;
        Vector3 projectedFacing = desiredFacing - Vector3.Dot(desiredFacing, localUp) * localUp;

        if (projectedFacing.LengthSquared() < epsilonSq)
        {
            //  Target is on the local up axis. Pick a deterministic perpendicular facing.
            return BuildAxialBasis(localUp, GetFallbackFacing(localUp));
        }

        projectedFacing = Vector3.Normalize(projectedFacing);
        return BuildAxialBasis(localUp, projectedFacing);
    }

    /// <summary>
    ///     Deterministic facing perpendicular to localUp for when the target aligns with the up axis,
    ///     so the result never depends on the raw roll.
    /// </summary>
    private static Vector3 GetFallbackFacing(Vector3 localUp)
    {
        Vector3 reference = MathF.Abs(Vector3.Dot(Vector3.UnitY, localUp)) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;
        Vector3 facing = reference - Vector3.Dot(reference, localUp) * localUp;
        return Vector3.Normalize(facing);
    }

    /// <summary>
    ///     Builds an orthonormal basis from up and facing and returns it as an orientation.
    /// </summary>
    private static Quaternion BuildAxialBasis(Vector3 localUp, Vector3 facing)
    {
        Vector3 right = Vector3.Cross(localUp, facing);
        Vector3 forward = Vector3.Cross(right, localUp);

        Matrix4x4 basisMatrix = new Matrix4x4(
            right.X, right.Y, right.Z, 0f,
            localUp.X, localUp.Y, localUp.Z, 0f,
            forward.X, forward.Y, forward.Z, 0f,
            0f, 0f, 0f, 1f
        );

        return Quaternion.CreateFromRotationMatrix(basisMatrix);
    }

    private readonly struct BillboardAction(CameraEntity camera, BillboardSystem system) : IForEach<BillboardComponent, TransformComponent>
    {
        public void Execute(float delta, DataStore store, int entity, in BillboardComponent billboard, in TransformComponent transform)
        {
            var record = new BillboardRecord(owner: store.GetUuid(entity), transform.Position, transform.Orientation, billboard.Offset, billboard.Size, billboard.Materials);
            system.AddOrUpdateBillboard(store, record, camera.Transform.Position);
        }
    }

    private struct BillboardRecord(
        Uuid owner,
        Vector3 position,
        Quaternion orientation,
        Vector3 offset,
        Vector2 size,
        Material[] materials
    ) {
        public readonly Uuid Owner = owner;
        public readonly Vector3 Position = position;
        public readonly Quaternion Orientation = orientation;
        public readonly Vector3 Offset = offset;
        public readonly Vector2 Size = size;
        public readonly Material[] Materials = materials;
    }

    private sealed class BillboardState(int entity, MeshRenderer renderer, Material[] directions, int activeIndex)
    {
        public readonly int Entity = entity;
        public MeshRenderer Renderer = renderer;
        public Material[] Directions = directions;
        public int ActiveIndex = activeIndex;
    }
}