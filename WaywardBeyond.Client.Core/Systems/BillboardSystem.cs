using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Graphics;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
///     Renders a camera-facing textured plane for any entity carrying a <see cref="BillboardComponent"/>.
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

        Vector3 position = record.Position + record.Offset;
        Quaternion orientation = GetAxialLookAt(cameraPosition, position, record.Orientation);
        var scale = new Vector3(record.Size.X, record.Size.Y, 1f);
        var transform = new TransformComponent(position, orientation, scale);

        if (!_billboards.TryGetValue(record.Owner, out BillboardState? state))
        {
            //  Create a billboard entity if it doesn't exist
            int entity = store.Alloc();
            var meshRenderer = new MeshRenderer(_mesh, record.Material, new RenderOptions { DoubleFaced = true });
            store.AddOrUpdate(entity, new MeshRendererComponent(meshRenderer));
            
            state = new BillboardState(entity, meshRenderer, record.Material);
            _billboards[record.Owner] = state;
        }
        else if (!ReferenceEquals(state.Material, record.Material))
        {
            //  Update the billboard's renderer if its material changed
            state.Renderer.Dispose();
            state.Renderer = new MeshRenderer(_mesh, record.Material, new RenderOptions { DoubleFaced = true });
            state.Material = record.Material;
            store.AddOrUpdate(state.Entity, new MeshRendererComponent(state.Renderer));
        }

        store.AddOrUpdate(state.Entity, transform);
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
            var record = new BillboardRecord(owner: store.GetUuid(entity), transform.Position, transform.Orientation, billboard.Offset, billboard.Size, billboard.Material);
            system.AddOrUpdateBillboard(store, record, camera.Transform.Position);
        }
    }

    private struct BillboardRecord(
        Uuid owner,
        Vector3 position,
        Quaternion orientation,
        Vector3 offset,
        Vector2 size,
        Material material
    ) {
        public readonly Uuid Owner = owner;
        public readonly Vector3 Position = position;
        public readonly Quaternion Orientation = orientation;
        public readonly Vector3 Offset = offset;
        public readonly Vector2 Size = size;
        public readonly Material Material = material;
    }

    private sealed class BillboardState(int entity, MeshRenderer renderer, Material material)
    {
        public readonly int Entity = entity;
        public MeshRenderer Renderer = renderer;
        public Material Material = material;
    }
}