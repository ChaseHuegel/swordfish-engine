using System.Collections.Concurrent;
using Swordfish.Audio;
using Swordfish.ECS;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class AudioChannelSystem(in VolumeSettings volumeSettings) : IEntitySystem
{
    private readonly VolumeSettings _volumeSettings = volumeSettings;

    private int? _masterChannel;
    private int? _effectsChannel;
    private int? _interfaceChannel;
    private int? _musicChannel;

    private readonly ConcurrentDictionary<string, Uuid> _channelEntities = [];

    //  Pending audio plays enqueued off the ECS thread (e.g. UI/menu sounds) and allocated into the store
    //  here on the ECS thread, so store mutations never happen off-thread.
    private readonly ConcurrentQueue<AudioPlay> _pendingPlays = new();

    public bool TryGetChannelEntity(string name, out Uuid channel)
    {
        return _channelEntities.TryGetValue(name, out channel);
    }

    public void EnqueuePlay(in AudioSource audioSource, in AudioPlayer audioPlayer)
    {
        _pendingPlays.Enqueue(new AudioPlay(audioSource, audioPlayer));
    }
    
    public void Tick(float delta, DataStore store)
    {
        //  Create channels
        if (_masterChannel == null)
        {
            _masterChannel = store.Alloc(new IdentifierComponent(name: "master", tag: "audio"), new AudioChannel(_volumeSettings.Master.Get()));
            _channelEntities["master"] = store.GetUuid(_masterChannel.Value);
        }
        
        if (_effectsChannel == null)
        {
            _effectsChannel = store.Alloc(new IdentifierComponent(name: "effects", tag: "audio"), new AudioChannel(_volumeSettings.Effects.Get()));
            _channelEntities["effects"] = store.GetUuid(_effectsChannel.Value);
        }
        
        if (_interfaceChannel == null)
        {
            _interfaceChannel = store.Alloc(new IdentifierComponent(name: "interface", tag: "audio"), new AudioChannel(_volumeSettings.Interface.Get()));
            _channelEntities["interface"] = store.GetUuid(_interfaceChannel.Value);
        }
        
        if (_musicChannel == null)
        {
            _musicChannel = store.Alloc(new IdentifierComponent(name: "music", tag: "audio"), new AudioChannel(_volumeSettings.Music.Get()));
            _channelEntities["music"] = store.GetUuid(_musicChannel.Value);
        }
        
        //  Update channels
        float masterVolume = _volumeSettings.Master.Get();
        store.AddOrUpdate(_masterChannel.Value, new AudioChannel(masterVolume));
        store.AddOrUpdate(_effectsChannel.Value, new AudioChannel(_volumeSettings.Effects.Get() * masterVolume));
        store.AddOrUpdate(_interfaceChannel.Value, new AudioChannel(_volumeSettings.Interface.Get() * masterVolume));
        store.AddOrUpdate(_musicChannel.Value, new AudioChannel(_volumeSettings.Music.Get() * masterVolume));

        //  Allocate queued audio plays on the ECS thread
        while (_pendingPlays.TryDequeue(out AudioPlay play))
        {
            store.Alloc(play.AudioSource, play.AudioPlayer);
        }
    }

    private readonly struct AudioPlay(in AudioSource audioSource, in AudioPlayer audioPlayer)
    {
        public readonly AudioSource AudioSource = audioSource;
        public readonly AudioPlayer AudioPlayer = audioPlayer;
    }
}