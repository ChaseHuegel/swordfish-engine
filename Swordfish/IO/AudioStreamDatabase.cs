using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Audio;
using Swordfish.Library.IO;

namespace Swordfish.IO;

/// <summary>
///     Provides access to <see cref="AudioStream"/>s from virtual resources.
/// </summary>
internal sealed class AudioStreamDatabase : SimpleVirtualAssetDatabase<AudioStream>, IAutoActivate
{
    public AudioStreamDatabase(
        in ILogger<AudioStreamDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs
    ) : base(logger, fileParseService, vfs)
    {
        Load();
    }
    
    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".wav");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Audio;
}