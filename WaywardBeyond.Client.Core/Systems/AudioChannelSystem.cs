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

    private readonly ConcurrentDictionary<string, int> _channelEntities = [];

    public bool TryGetChannelEntity(string name, out int channel)
    {
        return _channelEntities.TryGetValue(name, out channel);
    }
    
    public void Tick(float delta, DataStore store)
    {
        //  Create channels
        if (_masterChannel == null)
        {
            _masterChannel = store.Alloc(new IdentifierComponent(name: "master", tag: "audio"), new AudioChannel(_volumeSettings.Master.Get()));
            _channelEntities["master"] = _masterChannel.Value;
        }
        
        if (_effectsChannel == null)
        {
            _effectsChannel = store.Alloc(new IdentifierComponent(name: "effects", tag: "audio"), new AudioChannel(_volumeSettings.Effects.Get()));
            _channelEntities["effects"] = _effectsChannel.Value;
        }
        
        if (_interfaceChannel == null)
        {
            _interfaceChannel = store.Alloc(new IdentifierComponent(name: "interface", tag: "audio"), new AudioChannel(_volumeSettings.Interface.Get()));
            _channelEntities["interface"] = _interfaceChannel.Value;
        }
        
        if (_musicChannel == null)
        {
            _musicChannel = store.Alloc(new IdentifierComponent(name: "music", tag: "audio"), new AudioChannel(_volumeSettings.Music.Get()));
            _channelEntities["music"] = _musicChannel.Value;
        }
        
        //  Update channels
        float masterVolume = _volumeSettings.Master.Get();
        store.AddOrUpdate(_masterChannel.Value, new AudioChannel(masterVolume));
        store.AddOrUpdate(_effectsChannel.Value, new AudioChannel(_volumeSettings.Effects.Get() * masterVolume));
        store.AddOrUpdate(_interfaceChannel.Value, new AudioChannel(_volumeSettings.Interface.Get() * masterVolume));
        store.AddOrUpdate(_musicChannel.Value, new AudioChannel(_volumeSettings.Music.Get() * masterVolume));
    }
}