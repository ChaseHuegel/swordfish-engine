using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Services;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Player-specific glue between the replicated <see cref="BodyViewComponent"/> (a body asset ID) and
/// the general client billboard path. For every remote player entity (an entity carrying a
/// <see cref="BodyViewComponent"/> but no local <see cref="PlayerComponent"/>), it resolves the body ID
/// into world-space directional materials (the UI keeps the standing pose; remote players billboard
/// hovering) and attaches a <see cref="BillboardComponent"/> with those materials, which the general
/// <see cref="BillboardSystem"/> renders. Caches the material set per body so remote players share
/// textures; leaves the local player untouched.
/// </summary>
internal sealed class RemotePlayerVisualSystem : IEntitySystem
{
    private readonly CharacterAssetService _characterAssetService;

    private readonly Dictionary<string, Material[]> _materials = [];

    public RemotePlayerVisualSystem(in CharacterAssetService characterAssetService)
    {
        _characterAssetService = characterAssetService;
    }

    public void Tick(float delta, DataStore store)
    {
        AttachAction action = new() { Owner = this, Store = store };
        store.Query<BodyViewComponent, TransformComponent, AttachAction>(delta, ref action);
    }

    private Material[] ResolveMaterials(string bodyId)
    {
        if (_materials.TryGetValue(bodyId, out Material[]? cached))
        {
            return cached;
        }

        Material[] materials = _characterAssetService.GetFloatingMaterials(bodyId);
        if (materials.Length > 0)
        {
            _materials[bodyId] = materials;
        }

        return materials;
    }

    private void Attach(DataStore store, int entity, in BodyViewComponent body, in TransformComponent transform)
    {
        //  Never billboard the local player: it has PlayerComponent and is rendered from first person.
        if (store.TryGet(entity, out PlayerComponent _))
        {
            return;
        }

        Material[] materials = ResolveMaterials(body.Body);

        //  Scale the quad to the texture's aspect so the sprite is not squashed, sized to the player's body height.
        Texture texture = materials[0].Textures[0];
        float height = PlayerBodyConfig.PLAYER_STANDING_HEIGHT * transform.Scale.Y;
        float width = texture.Width > 0 ? height * (texture.Width / (float)texture.Height) : height;

        store.AddOrUpdate(entity, new BillboardComponent
        {
            Offset = new Vector3(0f, PlayerBodyConfig.PLAYER_FLYING_EYE_OFFSET, 0f),
            Size = new Vector2(width, height),
            Materials = materials,
        });
    }

    private struct AttachAction : IForEach<BodyViewComponent, TransformComponent>
    {
        public RemotePlayerVisualSystem Owner;
        public DataStore Store;

        public void Execute(float delta, DataStore store, int entity, in BodyViewComponent body, in TransformComponent transform)
        {
            Owner.Attach(store, entity, body, transform);
        }
    }
}