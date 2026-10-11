using Swordfish.Library.Configuration;
using Swordfish.Library.IO;
using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>Storage configuration.</summary>
public sealed class StorageConfig : Config<StorageConfig>
{
    /// <summary>
    /// The save data root path.
    /// </summary>
    public DataBinding<PathInfo> SaveRoot { get; private set; } = new("saves/");
}
