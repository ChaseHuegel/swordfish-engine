using Swordfish.Library.Types;

namespace Swordfish.Settings;

public sealed record PlaybackSettings
{
    public DataBinding<int> SampleRate { get; private set; } = new(48000);
    public DataBinding<int> Channels { get; private set; } = new(2);
}