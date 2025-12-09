using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Client.Core.Configuration;

public sealed class VolumeSettings : Config<VolumeSettings>
{
    public DataBinding<float> Master { get; private set; } = new(0.5f);
}