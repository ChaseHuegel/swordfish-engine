using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Client.Skills;

/// <summary>Resolves and caches UI icon materials for skills, falling back to the unknown skill icon.</summary>
internal sealed class SkillIconDatabase : IDisposable
{
    internal const string UNKNOWN_ICON = "skills/unknown.png";

    private readonly ILogger<SkillIconDatabase> _logger;
    private readonly IAssetDatabase<Texture> _textures;
    private readonly Shader? _ownedShader;
    private readonly Material _unknownIcon;
    private readonly Dictionary<string, Material> _icons = new(StringComparer.Ordinal);
    private readonly List<Texture> _ownedTextures = [];
    private readonly List<Material> _ownedMaterials = [];

    /// <summary>Creates the skill icon cache using the shared UI texture shader.</summary>
    public SkillIconDatabase(
        in ILogger<SkillIconDatabase> logger,
        in IFileParseService fileParseService,
        in IAssetDatabase<Texture> textures
    )
    {
        _logger = logger;
        _textures = textures;
        _ownedShader = fileParseService.Parse<Shader>(AssetPaths.Shaders.At("ui_reef_textured.glsl"));
        _unknownIcon = CreateUnknownMaterial(_ownedShader);
    }

    /// <summary>Creates the skill icon cache with a caller-supplied UI shader for headless tests.</summary>
    internal SkillIconDatabase(
        in ILogger<SkillIconDatabase> logger,
        in Shader uiShader,
        in IAssetDatabase<Texture> textures
    )
    {
        _logger = logger;
        _textures = textures;
        _unknownIcon = CreateUnknownMaterial(uiShader);
    }

    /// <summary>Returns the icon material for a skill, falling back to <see cref="UNKNOWN_ICON"/>.</summary>
    public Material Get(Skill skill)
    {
        string? iconPath = skill.Icon;
        if (string.IsNullOrWhiteSpace(iconPath) || string.Equals(iconPath, UNKNOWN_ICON, StringComparison.Ordinal))
        {
            return _unknownIcon;
        }

        if (_icons.TryGetValue(iconPath, out Material? cached))
        {
            return cached;
        }

        Result<Texture> textureResult = _textures.Get(iconPath);
        if (!textureResult.Success)
        {
            _logger.LogWarning(
                "Failed to resolve icon \"{icon}\" for skill \"{skill}\"; falling back to \"{fallback}\".",
                iconPath,
                skill.ID,
                UNKNOWN_ICON
            );
            _icons[iconPath] = _unknownIcon;
            return _unknownIcon;
        }

        var icon = new Material(_unknownIcon.Shader, textureResult.Value);
        _icons[iconPath] = icon;
        _ownedMaterials.Add(icon);
        return icon;
    }

    private Material CreateUnknownMaterial(Shader uiShader)
    {
        Result<Texture> unknownResult = _textures.Get(UNKNOWN_ICON);
        if (unknownResult.Success)
        {
            return new Material(uiShader, unknownResult.Value);
        }

        _logger.LogWarning("Failed to load fallback skill icon \"{icon}\".", UNKNOWN_ICON);
        var fallback = new Texture("skills/unknown_fallback", new byte[4], 1, 1, mipmaps: false);
        _ownedTextures.Add(fallback);
        return new Material(uiShader, fallback);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        for (var i = 0; i < _ownedMaterials.Count; i++)
        {
            _ownedMaterials[i].Dispose();
        }
        _ownedMaterials.Clear();

        for (var i = 0; i < _ownedTextures.Count; i++)
        {
            _ownedTextures[i].Dispose();
        }
        _ownedTextures.Clear();

        _unknownIcon.Dispose();
        _ownedShader?.Dispose();
    }
}
