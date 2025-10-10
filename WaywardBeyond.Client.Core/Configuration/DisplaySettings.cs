using Swordfish.Library.Types;

namespace WaywardBeyond.Client.Core.Configuration;

public sealed class DisplaySettings
{
    public DataBinding<bool> Fullscreen { get; private set; } = new();
}