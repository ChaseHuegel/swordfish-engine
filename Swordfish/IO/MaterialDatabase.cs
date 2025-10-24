using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace Swordfish.IO;

/// <summary>
///     Provides access to <see cref="Texture"/>s from virtual resources.
/// </summary>
internal sealed class MaterialDatabase : ResourceVirtualAssetDatabase<MaterialDefinition, Material>, IAutoActivate
{
    private readonly IAssetDatabase<Texture> _textureDatabase;
    private readonly IAssetDatabase<Shader> _shaderDatabase;

    public MaterialDatabase(
        in ILogger<MaterialDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs,
        in IAssetDatabase<Texture> textureDatabase,
        in IAssetDatabase<Shader> shaderDatabase
    ) : base(logger, fileParseService, vfs)
    {
        _textureDatabase = textureDatabase;
        _shaderDatabase = shaderDatabase;
        Load();
    }
    
    /// <inheritdoc/>
    protected override bool ExcludeExtensionFromID => true;

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");
    
    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Materials;
    
    /// <inheritdoc/>
    protected override Result<Material> LoadAsset(string id, Resource<MaterialDefinition> assetInfo)
    {
        Result<Shader> shader = _shaderDatabase.Get(assetInfo.Value.Shader);
        if (!shader)
        {
            return new Result<Material>(success: false, null!, shader.Message, shader.Exception);
        }

        var textures = new Texture[assetInfo.Value.Textures.Length];
        for (var i = 0; i < textures.Length; i++)
        {
            Result<Texture> texture = _textureDatabase.Get(assetInfo.Value.Textures[i]);
            if (!texture)
            {
                return new Result<Material>(success: false, null!, texture.Message, texture.Exception);
            }

            textures[i] = texture;
        }
        
        var material = new Material(shader, textures);
        material.Transparent = assetInfo.Value.Transparent;
        return Result<Material>.FromSuccess(material);
    }
}