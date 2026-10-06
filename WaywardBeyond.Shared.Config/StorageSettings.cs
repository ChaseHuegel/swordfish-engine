using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Shared.Config;

/// <summary>
/// Save-data root configuration. The root is relative to the process working directory unless set to
/// an absolute path.
/// </summary>
public sealed class StorageSettings : Config<StorageSettings>
{
    public DataBinding<string> DataRoot { get; private set; } = new("saves/");
}
