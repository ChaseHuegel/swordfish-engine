using System.IO;
using System.Linq;
using Swordfish.Audio;
using Swordfish.ECS;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Systems;

namespace WaywardBeyond.Client.Core.Services;

internal sealed class SoundEffectService
{
    private readonly AudioChannelSystem _audioChannelSystem;
    private readonly IECSContext _ecsContext;

    private readonly Randomizer _randomizer;
    private readonly string[] _placeMetalSounds;
    private readonly string[] _removeMetalSounds;

    public SoundEffectService(in AudioChannelSystem audioChannelSystem, in IECSContext ecsContext, in VirtualFileSystem vfs)
    {
        _audioChannelSystem = audioChannelSystem;
        _ecsContext = ecsContext;
        _randomizer = new Randomizer();
        
        PathInfo placeMetalFolder = AssetPaths.Audio.At("sounds/place/metal/");
        _placeMetalSounds = vfs.GetFiles(placeMetalFolder, SearchOption.TopDirectoryOnly)
            .Select(pathInfo => $"sounds/place/metal/{pathInfo.GetFileName()}")
            .ToArray();
        
        PathInfo removeMetalFolder = AssetPaths.Audio.At("sounds/remove/metal/");
        _removeMetalSounds = vfs.GetFiles(removeMetalFolder, SearchOption.TopDirectoryOnly)
            .Select(pathInfo => $"sounds/remove/metal/{pathInfo.GetFileName()}")
            .ToArray();
    }

    public void Play(string id, string channelName)
    {
        if (!_audioChannelSystem.TryGetChannelEntity(channelName, out int channel))
        {
            return;
        }
        
        var audioSource = new AudioSource(id);
        Play(audioSource, channel);
    }

    public void PlayPlaceMetal()
    {
        if (!_audioChannelSystem.TryGetChannelEntity("effects", out int channel))
        {
            return;
        }
        
        var audioSource = new AudioSource(id: _randomizer.Select(_placeMetalSounds));
        Play(audioSource, channel);
    }
    
    public void PlayRemoveMetal()
    {
        if (!_audioChannelSystem.TryGetChannelEntity("effects", out int channel))
        {
            return;
        }
        
        var audioSource = new AudioSource(id: _randomizer.Select(_removeMetalSounds));
        Play(audioSource, channel);
    }

    private void Play(AudioSource audioSource, int channelEntity)
    {
        var audioPlayer = new AudioPlayer
        {
            Volume = 1f,
            Pitch = 1f,
            Loop = false,
            PlayOnce = true,
            State = PlayerState.Play,
            ChannelEntity = channelEntity,
        };
        _ecsContext.World.DataStore.Alloc(audioSource, audioPlayer);
    }
}