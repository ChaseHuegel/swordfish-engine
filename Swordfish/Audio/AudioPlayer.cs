using Swordfish.ECS;

namespace Swordfish.Audio;

public struct AudioPlayer(int channelEntity) : IDataComponent
{
    public float Volume = 1f;
    public float Pitch = 1f;
    public bool Loop;
    public bool PlayOnce;
    public PlayerState State;
    public int ChannelEntity = channelEntity;
}