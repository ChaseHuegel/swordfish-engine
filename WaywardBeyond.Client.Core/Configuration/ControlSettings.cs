using Swordfish.Library.Types;

namespace WaywardBeyond.Client.Core.Configuration;

public sealed class ControlSettings
{
    public DataBinding<int> LookSensitivity { get; private set; } = new(5);
}