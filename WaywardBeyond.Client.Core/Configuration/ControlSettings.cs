using Swordfish.Library.Types;

namespace WaywardBeyond.Client.Core.Configuration;

internal sealed class ControlSettings
{
    public DataBinding<int> LookSensitivity { get; } = new(5);
}