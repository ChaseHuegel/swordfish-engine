using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Client.Core.Configuration;

public sealed class ControlSettings : Config<ControlSettings>
{
    public DataBinding<int> LookSensitivity { get; private set; } = new(5);
    public DataBinding<bool> RememberShape { get; private set; } = new(true);
    public DataBinding<bool> RememberOrientation { get; private set; } = new(true);
}