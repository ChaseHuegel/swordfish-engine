using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.Graphics.Particles;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace Swordfish.IO;

/// <summary>Provides access to <see cref="ParticleEffect"/> assets loaded from virtual resources.</summary>
internal sealed class ParticleEffectDatabase : ResourceVirtualAssetDatabase<ParticleEffectDefinition, ParticleEffect>, IAutoActivate
{
    private readonly IAssetDatabase<Texture> _textureDatabase;

    /// <summary>Constructs and loads the particle effect asset database.</summary>
    public ParticleEffectDatabase(
        in ILogger<ParticleEffectDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs,
        in IAssetDatabase<Texture> textureDatabase
    ) : base(logger, fileParseService, vfs)
    {
        _textureDatabase = textureDatabase;
        Load();
    }

    /// <inheritdoc/>
    protected override bool ExcludeExtensionFromID => true;

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Particles;

    /// <inheritdoc/>
    protected override Result<ParticleEffect> LoadAsset(string id, Resource<ParticleEffectDefinition> assetInfo)
    {
        Texture? texture = null;
        if (!string.IsNullOrWhiteSpace(assetInfo.Value.Texture))
        {
            Result<Texture> textureResult = _textureDatabase.Get(assetInfo.Value.Texture);
            if (!textureResult.Success)
            {
                return new Result<ParticleEffect>(success: false, null!, textureResult.Message, textureResult.Exception);
            }

            texture = textureResult.Value;
        }

        return assetInfo.Value.Build(id, texture);
    }
}
