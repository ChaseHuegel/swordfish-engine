using Swordfish.ECS;

namespace Swordfish.Audio;

public struct AudioChannel(float volume, string? playbackDevice = null) : IDataComponent
{
    public float Volume = volume;
    public string? PlaybackDevice = playbackDevice;
}