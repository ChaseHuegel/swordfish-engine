using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace Swordfish.Settings;

public sealed class ECSSettings : Config<ECSSettings>
{
    public DataBinding<int> TargetTPS { get; private set; } = new(128);
}