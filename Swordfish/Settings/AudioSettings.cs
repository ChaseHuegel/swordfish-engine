using Swordfish.Library.Configuration;

namespace Swordfish.Settings;

public sealed class AudioSettings : Config<AudioSettings>
{
    public PlaybackSettings Playback { get; } = new();
}