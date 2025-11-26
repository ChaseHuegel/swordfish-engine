using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Audio;
using Swordfish.Library.IO;

namespace Swordfish.IO;

/// <summary>
///     Provides access to <see cref="AudioSource"/>s from virtual resources.
/// </summary>
internal sealed class AudioSourceDatabase : SimpleVirtualAssetDatabase<AudioSource>, IAutoActivate
{
    public AudioSourceDatabase(
        in ILogger<AudioSourceDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs)
        : base(logger, fileParseService, vfs)
    {
        Load();
    }
    
    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".wav");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Audio;
}