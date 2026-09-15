using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Graphics;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Renders any entity carrying a <see cref="BillboardComponent"/> as a camera-facing textured plane.
/// General and reusable — not player-specific. For each such entity it maintains a standalone quad
/// companion entity that tracks the owner's position (plus the billboard offset), is oriented toward the
/// client camera every frame, and is textured with the owner's billboard material. The companion is
/// disposed and freed when the owner stops carrying the component or is removed.
/// </summary>
public sealed class BillboardSystem(IRenderContext renderContext) : IEntitySystem
{
    private readonly Dictionary<Uuid, Slot> _slots = [];
    private readonly HashSet<Uuid> _seen = [];
    private readonly List<Uuid> _stale = [];
    private readonly List<BillboardRecord> _records = [];
    private readonly Billboard _mesh = new();

    public void Tick(float delta, DataStore store)
    {
        _seen.Clear();
        _records.Clear();

        CollectAction collect = new() { Records = _records };

        store.Query<BillboardComponent, TransformComponent, CollectAction>(delta, ref collect);

        CameraEntity camera = renderContext.MainCamera.Get();
        Vector3 cameraPosition = camera.Transform.Position;
        foreach (BillboardRecord record in _records)
        {
            EnsureBillboard(store, record, cameraPosition);
        }

        //  Clean up companions whose owner no longer renders a billboard.
        _stale.Clear();
        foreach (Uuid owner in _slots.Keys)
        {
            if (!_seen.Contains(owner))
            {
                _stale.Add(owner);
            }
        }

        foreach (Uuid owner in _stale)
        {
            Slot slot = _slots[owner];
            slot.Renderer.Dispose();
            store.Free(slot.Entity);
            _slots.Remove(owner);
        }
    }

    private void EnsureBillboard(DataStore store, BillboardRecord record, Vector3 cameraPosition)
    {
        _seen.Add(record.Owner);

        Vector3 position = record.Position + record.Offset;
        Quaternion billboardOrientation = GetAxialLookAtRotation(position, cameraPosition, record.Orientation);

        if (!_slots.TryGetValue(record.Owner, out Slot? slot))
        {
            var meshRenderer = new MeshRenderer(_mesh, record.Material, new RenderOptions { DoubleFaced = true });
            int entity = store.Alloc();
            store.AddOrUpdate(entity, new TransformComponent(position, billboardOrientation, new Vector3(record.Size.X, record.Size.Y, 1f)));
            store.AddOrUpdate(entity, new MeshRendererComponent(meshRenderer));
            _slots[record.Owner] = new Slot(entity, meshRenderer, record.Material);
            return;
        }

        if (!ReferenceEquals(record.Material, slot.Material))
        {
            slot.Renderer.Dispose();
            slot.Renderer = new MeshRenderer(_mesh, record.Material, new RenderOptions { DoubleFaced = true });
            slot.Material = record.Material;
            store.AddOrUpdate(slot.Entity, new MeshRendererComponent(slot.Renderer));
        }

        store.AddOrUpdate(slot.Entity, new TransformComponent(
            position,
            billboardOrientation,
            new Vector3(record.Size.X, record.Size.Y, 1f)
        ));
    }

    private struct BillboardRecord
    {
        public Uuid Owner;
        public Vector3 Position;
        public Quaternion Orientation;
        public Vector3 Offset;
        public Vector2 Size;
        public Material Material;

        public BillboardRecord(Uuid owner, Vector3 position, Quaternion orientation, Vector3 offset, Vector2 size, Material material)
        {
            Owner = owner;
            Position = position;
            Orientation = orientation;
            Offset = offset;
            Size = size;
            Material = material;
        }
    }

    private sealed class Slot
    {
        public int Entity;
        public MeshRenderer Renderer;
        public Material Material;

        public Slot(int entity, MeshRenderer renderer, Material material)
        {
            Entity = entity;
            Renderer = renderer;
            Material = material;
        }
    }

    private struct CollectAction : IForEach<BillboardComponent, TransformComponent>
    {
        public List<BillboardRecord> Records;

        public void Execute(float delta, DataStore store, int entity, in BillboardComponent billboard, in TransformComponent transform)
        {
            Records.Add(new BillboardRecord(store.GetUuid(entity), transform.Position, transform.Orientation, billboard.Offset, billboard.Size, billboard.Material));
        }
    }
    
    /// <summary>Rotates around the entity's local up axis to face the camera, keeping local up fixed.</summary>
    private static Quaternion GetAxialLookAtRotation(Vector3 position, Vector3 cameraPosition, Quaternion entityRotation)
    {
        const float epsilonSq = 1e-6f;

        //  Local up drives the axis the billboard spins about.
        Vector3 localUp = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, entityRotation));

        //  Facing toward the camera, projected onto the plane orthogonal to localUp.
        Vector3 desiredFacing = cameraPosition - position;
        Vector3 projectedFacing = desiredFacing - Vector3.Dot(desiredFacing, localUp) * localUp;

        //  Camera on the local up axis: pick a deterministic perpendicular facing.
        if (projectedFacing.LengthSquared() < epsilonSq)
        {
            return BuildAxialBasis(localUp, PickFallbackFacing(localUp));
        }

        projectedFacing = Vector3.Normalize(projectedFacing);
        return BuildAxialBasis(localUp, projectedFacing);
    }

    //  Deterministic facing perpendicular to localUp for when the camera aligns with the up axis, so the
    //  result never depends on the raw roll.
    private static Vector3 PickFallbackFacing(Vector3 localUp)
    {
        Vector3 reference = MathF.Abs(Vector3.Dot(Vector3.UnitY, localUp)) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;
        Vector3 facing = reference - Vector3.Dot(reference, localUp) * localUp;
        return Vector3.Normalize(facing);
    }

    //  Builds an orthonormal basis from up and facing and returns it as a rotation.
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
}