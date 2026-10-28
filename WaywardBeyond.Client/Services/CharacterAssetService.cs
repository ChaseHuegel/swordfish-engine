using System.Collections.Generic;
using System.Linq;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Bodies;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Services;

/// <summary>
/// Resolves a body's stable string ID (the <see cref="Character.Body"/> / <c>BodyViewComponent.Body</c>)
/// into renderable client materials and its species ID. Fronts (standing) drive the UI preview and face-forward material;
/// floating materials drive the directional remote-player billboard. An ID that is not a loaded body falls
/// back to the first loaded body so older or malformed saves still render.
/// </summary>
internal sealed class CharacterAssetService
{
    private readonly IBodyDatabase _bodyDatabase;
    private readonly IAssetDatabase<Texture> _textureDatabase;
    private readonly Shader _uiShader;
    private readonly Shader _worldShader;

    private readonly Dictionary<string, Material> _standingMaterials = [];
    private readonly Dictionary<string, Material[]> _floatingMaterials = [];

    public CharacterAssetService(
        in IBodyDatabase bodyDatabase,
        in IAssetDatabase<Texture> textureDatabase,
        in IFileParseService fileParseService
    ) {
        _bodyDatabase = bodyDatabase;
        _textureDatabase = textureDatabase;
        _uiShader = fileParseService.Parse<Shader>(AssetPaths.Shaders.At("ui_reef_textured.glsl"));
        _worldShader = fileParseService.Parse<Shader>(AssetPaths.Shaders.At("textured.glsl"));
    }

    /// <summary>Returns the body ID for a species and gender, falling back to the default body when the pair is unknown.</summary>
    public string GetBodyId(string species, Gender gender)
    {
        return _bodyDatabase.GetBody(species, gender)?.ID ?? _bodyDatabase.DefaultId ?? string.Empty;
    }

    /// <summary>Returns the species ID for a body ID, falling back to the default body for an unknown ID.</summary>
    public string GetSpecies(string bodyId)
    {
        Result<Body> body = _bodyDatabase.Get(ResolveId(bodyId));
        return body ? body.Value.Species : string.Empty;
    }

    /// <summary>Returns the standing (UI preview) material for a body ID, falling back to the default body for an unknown ID.</summary>
    public Material GetAppearanceMaterial(string bodyId)
    {
        return Resolve(bodyId, _standingMaterials, BuildStandingMaterial);
    }

    public Material GetAppearanceMaterial(Character character)
    {
        return GetAppearanceMaterial(character.Body);
    }

    /// <summary>
    /// Returns the ordered directional floating materials for a body ID, falling back to the default body
    /// for an unknown ID. Index 0 is the forward-facing (front) material and later indices step around the
    /// entity, so a billboard can select a sprite by relative facing.
    /// </summary>
    public Material[] GetFloatingMaterials(string bodyId)
    {
        return Resolve(bodyId, _floatingMaterials, BuildFloatingMaterials);
    }

    private string ResolveId(string bodyId)
    {
        return _bodyDatabase.Get(bodyId) ? bodyId : (_bodyDatabase.DefaultId ?? bodyId);
    }

    private Material BuildStandingMaterial(string bodyId)
    {
        string resolved = ResolveId(bodyId);
        //  Standing is the default state; if absent, fall back to the first floating state's texture.
        string[] textures = _bodyDatabase.Get(resolved).Value.GetTextures("standing");
        Texture texture = LoadFirstTexture(resolved, textures);
        return new Material(_uiShader, texture) { Transparent = true };
    }

    private Material[] BuildFloatingMaterials(string bodyId)
    {
        string resolved = ResolveId(bodyId);
        string[] textures = _bodyDatabase.Get(resolved).Value.GetTextures("floating");
        if (textures.Length == 0)
        {
            //  No floating pose: render the standing pose as a single-direction billboard.
            string texturePath = _bodyDatabase.Get(resolved).Value.GetTextures("standing").FirstOrDefault() ?? string.Empty;
            if (string.IsNullOrEmpty(texturePath))
            {
                return [];
            }

            return [new Material(_worldShader, LoadTexture(texturePath)) { Transparent = true }];
        }

        var materials = new Material[textures.Length];
        for (var i = 0; i < textures.Length; i++)
        {
            materials[i] = new Material(_worldShader, LoadTexture(textures[i])) { Transparent = true };
        }

        return materials;
    }

    private Texture LoadFirstTexture(string bodyId, string[] textures)
    {
        string texturePath = textures.FirstOrDefault() ?? string.Empty;
        return LoadTexture(texturePath);
    }

    private Texture LoadTexture(string path)
    {
        Result<Texture> textureResult = _textureDatabase.Get(path);
        return textureResult ? textureResult : _textureDatabase.Get("characters/human_m_standing.png");
    }

    private static T Resolve<T>(string bodyId, Dictionary<string, T> cache, System.Func<string, T> build)
    {
        if (!cache.TryGetValue(bodyId, out T? value))
        {
            value = build(bodyId);
            cache[bodyId] = value;
        }

        return value;
    }
}
