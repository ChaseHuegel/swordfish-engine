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
    
    private readonly string[] _placeRockSounds;
    private readonly string[] _removeRockSounds;

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
        
        PathInfo placeRockFolder = AssetPaths.Audio.At("sounds/place/rock/");
        _placeRockSounds = vfs.GetFiles(placeRockFolder, SearchOption.TopDirectoryOnly)
            .Select(pathInfo => $"sounds/place/rock/{pathInfo.GetFileName()}")
            .ToArray();
        
        PathInfo removeRockFolder = AssetPaths.Audio.At("sounds/remove/rock/");
        _removeRockSounds = vfs.GetFiles(removeRockFolder, SearchOption.TopDirectoryOnly)
            .Select(pathInfo => $"sounds/remove/rock/{pathInfo.GetFileName()}")
            .ToArray();
    }

    public void Play(string id, string channelName)
    {
        if (!_audioChannelSystem.TryGetChannelEntity(channelName, out Uuid channel))
        {
            return;
        }
        
        var audioSource = new AudioSource(id);
        Play(audioSource, channel);
    }

    public void PlayPlaceMetal()
    {
        PlayRandomSound(_placeMetalSounds);
    }
    
    public void PlayRemoveMetal()
    {
        PlayRandomSound(_removeMetalSounds);
    }
    
    public void PlayPlaceRock()
    {
        PlayRandomSound(_placeRockSounds);
    }
    
    public void PlayRemoveRock()
    {
        PlayRandomSound(_removeRockSounds);
    }
    
    private void PlayRandomSound(string[] ids)
    {
        if (!_audioChannelSystem.TryGetChannelEntity("effects", out Uuid channel))
        {
            return;
        }
        
        var audioSource = new AudioSource(id: _randomizer.Select(ids));
        Play(audioSource, channel);
    }

    private void Play(AudioSource audioSource, Uuid channelEntity)
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