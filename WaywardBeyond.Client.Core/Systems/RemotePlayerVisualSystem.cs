using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Services;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Player-specific glue between the replicated <see cref="BodyViewComponent"/> (an appearance index) and
/// the general client billboard path. For every remote player entity (an entity carrying a
/// <see cref="BodyViewComponent"/> but no local <see cref="PlayerComponent"/>), it resolves the Body index
/// into a world-space material (the same character texture the character UI uses, driven by the same
/// <see cref="BodyViewComponent.Body"/> index) and attaches a <see cref="BillboardComponent"/>, which the
/// general <see cref="BillboardSystem"/> renders. Caches one material per Body so remote players share
/// textures; leaves the local player untouched.
/// </summary>
internal sealed class RemotePlayerVisualSystem : IEntitySystem
{
    private static readonly float StandingHeight = 1.8f;

    private readonly CharacterAssetService _characterAssetService;
    private readonly Shader _texturedShader;

    private readonly Dictionary<int, Material> _materials = [];

    public RemotePlayerVisualSystem(
        in CharacterAssetService characterAssetService,
        in IFileParseService fileParseService
    ) {
        _characterAssetService = characterAssetService;
        _texturedShader = fileParseService.Parse<Shader>(AssetPaths.Shaders.At("textured.glsl"));
    }

    public void Tick(float delta, DataStore store)
    {
        AttachAction action = new() { Owner = this, Store = store };
        store.Query<BodyViewComponent, TransformComponent, AttachAction>(delta, ref action);
    }

    private Material ResolveMaterial(int body)
    {
        if (_materials.TryGetValue(body, out Material? cached))
        {
            return cached;
        }

        //  The character UI material carries the texture but renders through a clip-space Reef UI shader
        //  that cannot be drawn in-world, so reuse its texture under the world texture shader.
        Material appearance = _characterAssetService.GetAppearanceMaterial(body);
        Texture texture = appearance.Textures[0];
        var material = new Material(_texturedShader, texture) { Transparent = appearance.Transparent };
        _materials[body] = material;
        return material;
    }

    private void Attach(DataStore store, int entity, in BodyViewComponent body)
    {
        //  Never billboard the local player: it has PlayerComponent and is rendered from first person.
        if (store.TryGet(entity, out PlayerComponent _))
        {
            return;
        }

        if (!store.TryGet(entity, out TransformComponent _))
        {
            return;
        }

        Material material = ResolveMaterial(body.Body);

        //  Scale the quad to the texture's aspect so the sprite is not squashed, sized as a standing figure.
        Texture texture = material.Textures[0];
        float height = StandingHeight;
        float width = texture.Width > 0 ? height * (texture.Width / (float)texture.Height) : height;

        store.AddOrUpdate(entity, new BillboardComponent
        {
            Parent = store.GetUuid(entity),
            Offset = new Vector3(0f, StandingHeight * 0.5f, 0f),
            Size = new Vector2(width, height),
            Material = material,
        });
    }

    private struct AttachAction : IForEach<BodyViewComponent, TransformComponent>
    {
        public RemotePlayerVisualSystem Owner;
        public DataStore Store;

        public void Execute(float delta, DataStore store, int entity, in BodyViewComponent body, in TransformComponent transform)
        {
            Owner.Attach(store, entity, body);
            _ = transform;
        }
    }
}