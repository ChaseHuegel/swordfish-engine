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
public sealed class BillboardSystem : IEntitySystem
{
    private readonly Dictionary<Uuid, Slot> _slots = [];
    private readonly HashSet<Uuid> _seen = [];
    private Billboard? _mesh;

    public void Tick(float delta, DataStore store)
    {
        _mesh ??= new Billboard();
        _seen.Clear();

        //  Camera position drives the billboard orientation.
        ReadCameraAction readCamera = default;
        store.QueryRef<CameraComponent, TransformComponent, ReadCameraAction>(0f, ref readCamera);

        //  Collect owners first; creating companion entities during a Store query iteration is unsafe.
        CollectAction collect = new() { Records = [], CameraPosition = readCamera.CameraPosition };
        store.Query<BillboardComponent, TransformComponent, CollectAction>(delta, ref collect);

        foreach (BillboardRecord record in collect.Records)
        {
            EnsureBillboard(store, record, readCamera.CameraPosition);
        }

        //  Clean up companions whose owner no longer renders a billboard.
        foreach (Uuid owner in new List<Uuid>(_slots.Keys))
        {
            if (_seen.Contains(owner))
            {
                continue;
            }

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

        if (!_slots.TryGetValue(record.Owner, out Slot? slot))
        {
            var meshRenderer = new MeshRenderer(_mesh!, record.Material, new RenderOptions { DoubleFaced = true });
            int entity = store.Alloc();
            store.AddOrUpdate(entity, new TransformComponent(position, FaceCamera(position, cameraPosition), new Vector3(record.Size.X, record.Size.Y, 1f)));
            store.AddOrUpdate(entity, new MeshRendererComponent(meshRenderer));
            _slots[record.Owner] = new Slot(entity, meshRenderer, record.Material);
            return;
        }

        if (!ReferenceEquals(record.Material, slot.Material))
        {
            slot.Renderer.Dispose();
            slot.Renderer = new MeshRenderer(_mesh!, record.Material, new RenderOptions { DoubleFaced = true });
            slot.Material = record.Material;
            store.AddOrUpdate(slot.Entity, new MeshRendererComponent(slot.Renderer));
        }

        store.AddOrUpdate(slot.Entity, new TransformComponent(
            position,
            FaceCamera(position, cameraPosition),
            new Vector3(record.Size.X, record.Size.Y, 1f)
        ));
    }

    private static Quaternion FaceCamera(Vector3 position, Vector3 cameraPosition)
    {
        Vector3 toCamera = cameraPosition - position;
        if (toCamera.LengthSquared() < 1e-6f)
        {
            return Quaternion.Identity;
        }

        Vector3 forward = Vector3.Normalize(toCamera);
        Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
        if (right.LengthSquared() < 1e-6f)
        {
            right = Vector3.UnitX;
        }

        Vector3 up = Vector3.Cross(forward, right);
        var basis = new Matrix4x4(
            right.X, up.X, forward.X, 0f,
            right.Y, up.Y, forward.Y, 0f,
            right.Z, up.Z, forward.Z, 0f,
            0f, 0f, 0f, 1f
        );
        return Quaternion.CreateFromRotationMatrix(basis);
    }

    private struct BillboardRecord
    {
        public Uuid Owner;
        public Vector3 Position;
        public Vector3 Offset;
        public Vector2 Size;
        public Material Material;

        public BillboardRecord(Uuid owner, Vector3 position, Vector3 offset, Vector2 size, Material material)
        {
            Owner = owner;
            Position = position;
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

    private struct ReadCameraAction : IForEachRef<CameraComponent, TransformComponent>
    {
        public Vector3 CameraPosition;

        public void Execute(float delta, DataStore store, int entity, ref Ref<CameraComponent> camera, ref Ref<TransformComponent> transform)
        {
            CameraPosition = transform.Read.Position;
        }
    }

    private struct CollectAction : IForEach<BillboardComponent, TransformComponent>
    {
        public List<BillboardRecord> Records;
        public Vector3 CameraPosition;

        public void Execute(float delta, DataStore store, int entity, in BillboardComponent billboard, in TransformComponent transform)
        {
            Records.Add(new BillboardRecord(store.GetUuid(entity), transform.Position, billboard.Offset, billboard.Size, billboard.Material));
        }
    }
}