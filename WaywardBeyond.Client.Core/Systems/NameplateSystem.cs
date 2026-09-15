using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.UI;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Projects remote players' head positions to screen space for the nameplate UI layer.
/// </summary>
internal sealed class NameplateSystem(
    NameplateSnapshot snapshot,
    IRenderContext renderContext,
    IWindowContext windowContext
) : IEntitySystem
{
    private const float NAMEPLATE_HEIGHT = 1.8f;
    private const float MAX_RENDER_DISTANCE = 12;
    private const int BASE_FONT_SIZE = 12;
    private const float REFERENCE_DISTANCE = 4f;
    private const int MIN_FONT_SIZE = 9;
    private const int MAX_FONT_SIZE = 40;

    private readonly List<NameplateInfo> _nameplates = [];

    public void Tick(float delta, DataStore store)
    {
        _nameplates.Clear();

        if (WaywardBeyond.GameState != GameState.Playing)
        {
            snapshot.Update(_nameplates);
            return;
        }

        Vector2 resolution = windowContext.Resolution;
        if (resolution.X <= 0f || resolution.Y <= 0f)
        {
            snapshot.Update(_nameplates);
            return;
        }

        CameraEntity camera = renderContext.MainCamera.Get();
        if (camera.Entity.Ptr == Entity.Null)
        {
            snapshot.Update(_nameplates);
            return;
        }

        Matrix4x4 view = camera.GetView();
        Matrix4x4 projection = camera.GetProjection(resolution.X / resolution.Y);
        Vector3 cameraPosition = camera.Transform.Position;

        CollectAction action = new()
        {
            Owner = this,
            View = view,
            Projection = projection,
            CameraPosition = cameraPosition,
            Resolution = resolution,
        };
        store.Query<BodyViewComponent, TransformComponent, CollectAction>(delta, ref action);

        snapshot.Update(_nameplates);
    }

    private static void Project(
        List<NameplateInfo> nameplates,
        DataStore store,
        int entity,
        in TransformComponent transform,
        string? name,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        in Vector3 cameraPosition,
        in Vector2 resolution
    ) {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        Vector3 head = transform.Position + new Vector3(0f, NAMEPLATE_HEIGHT, 0f);

        float depth = Vector3.Distance(cameraPosition, head);
        if (depth > MAX_RENDER_DISTANCE)
        {
            return;
        }

        Vector4 clip = Vector4.Transform(Vector4.Transform(new Vector4(head, 1f), view), projection);
        if (clip.W <= 0f)
        {
            return;
        }

        float ndcX = clip.X / clip.W;
        float ndcY = clip.Y / clip.W;
        if (ndcX is < -1f or > 1f || ndcY is < -1f or > 1f)
        {
            return;
        }

        nameplates.Add(new NameplateInfo(
            store.GetUuid(entity),
            name,
            x: (int)((ndcX * 0.5f + 0.5f) * resolution.X),
            y: (int)((1f - (ndcY * 0.5f + 0.5f)) * resolution.Y),
            ComputeFontSize(depth)
        ));
    }

    private static int ComputeFontSize(float depth)
    {
        if (depth <= 0f)
        {
            return MAX_FONT_SIZE;
        }

        int size = (int)(BASE_FONT_SIZE * REFERENCE_DISTANCE / depth);
        return Math.Clamp(size, MIN_FONT_SIZE, MAX_FONT_SIZE);
    }

    private struct CollectAction : IForEach<BodyViewComponent, TransformComponent>
    {
        public NameplateSystem Owner;
        public Matrix4x4 View;
        public Matrix4x4 Projection;
        public Vector3 CameraPosition;
        public Vector2 Resolution;

        public void Execute(
            float delta,
            DataStore store,
            int entity,
            in BodyViewComponent body,
            in TransformComponent transform
        ) {
            //  Never nameplate the local player: it carries PlayerComponent and is rendered from first person.
            if (store.TryGet(entity, out PlayerComponent _))
            {
                return;
            }

            string? name = store.TryGet(entity, out IdentifierComponent identifier) ? identifier.Name : null;
            Project(Owner._nameplates, store, entity, transform, name, View, Projection, CameraPosition, Resolution);
            _ = body;
        }
    }
}