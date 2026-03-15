using Microsoft.Extensions.Logging;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;
using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Settings;

namespace Swordfish.Audio.SoundFlow;

internal sealed class AudioSystem : IEntitySystem, IDisposable
{
    private readonly AudioEngine _engine;
    private readonly IAssetDatabase<AudioStream> _audioStreamDatabase;
    
    private readonly AudioFormat _format;
    private readonly AudioPlaybackDevice? _defaultPlaybackDevice;
    private readonly Dictionary<int, SoundPlayer> _soundPlayers = [];
    private readonly Dictionary<SoundPlayer, string> _soundPlayerSources = [];
    private readonly Dictionary<string, AudioPlaybackDevice> _playbackDevices = [];
    
    public AudioSystem(ILogger<AudioSystem> logger, AudioEngine engine, AudioSettings audioSettings, IAssetDatabase<AudioStream> audioStreamDatabase)
    {
        _engine = engine;
        _audioStreamDatabase = audioStreamDatabase;
        
        _format = new AudioFormat
        {
            Format = SampleFormat.F32,
            SampleRate = audioSettings.Playback.SampleRate,
            Channels = audioSettings.Playback.Channels,
        };

        //  Init all playback devices
        foreach (DeviceInfo deviceInfo in _engine.PlaybackDevices)
        {
            AudioPlaybackDevice playbackDevice = _engine.InitializePlaybackDevice(deviceInfo, _format);
            _playbackDevices[deviceInfo.Name] = playbackDevice;

            if (deviceInfo.IsDefault)
            {
                _defaultPlaybackDevice = playbackDevice;
            }
        }

        if (_defaultPlaybackDevice == null)
        {
            logger.LogWarning("No default playback device found.");
        }
    }
    
    public void Dispose()
    {
        _engine.Dispose();
        _defaultPlaybackDevice?.Dispose();
    }
    
    public void Tick(float delta, DataStore store)
    {
        store.Query<CleanupAudioPlayer>(delta, CleanupAudioPlayers);
        store.Query<AudioPlayer, AudioSource>(delta, TickAudioPlayers);
    }

    private void CleanupAudioPlayers(float delta, DataStore store, int entity, ref CleanupAudioPlayer component1)
    {
        if (!_soundPlayers.TryGetValue(entity, out SoundPlayer? soundPlayer))
        {
            return;
        }

        DisposeSoundPlayer(soundPlayer);
        _soundPlayers.Remove(entity);
        _soundPlayerSources.Remove(soundPlayer);
        store.Free(entity);
    }

    private void TickAudioPlayers(float delta, DataStore store, int entity, ref AudioPlayer audioPlayer, ref AudioSource audioSource)
    {
        if (!store.TryGet(audioPlayer.ChannelEntity, out AudioChannel channel))
        {
            //  The player isn't assigned to a valid channel
            return;
        }

        //  Resolve the playback device
        bool useDefaultPlaybackDevice = string.IsNullOrEmpty(channel.PlaybackDevice);
        AudioPlaybackDevice? playbackDevice;
        if (useDefaultPlaybackDevice)
        {
            playbackDevice = _defaultPlaybackDevice;
        }
        else if (!_playbackDevices.TryGetValue(channel.PlaybackDevice!, out playbackDevice))
        {
            playbackDevice = null;
        }
        
        if (playbackDevice == null)
        {
            //  The channel isn't assigned to a valid playback device
            return;
        }
        
        if (!playbackDevice.IsRunning)
        {
            playbackDevice.Start();
        }
        
        Library.Util.Result<AudioStream> audioStream = _audioStreamDatabase.Get(audioSource.ID);
        if (!audioStream.Success)
        {
            //  The audio source isn't assigned to a valid stream
            return;
        }
        
        //  Ensure there is an underlying SoundPlayer
        if (!_soundPlayers.TryGetValue(entity, out SoundPlayer? soundPlayer))
        {
            soundPlayer = CreateSoundPlayer(entity, audioStream);
            _soundPlayerSources[soundPlayer] = audioSource.ID;
        }
        
        //  Recreate the SoundPlayer if the AudioSource has changed
        if (_soundPlayerSources.TryGetValue(soundPlayer, out string? previousAudioSource) && previousAudioSource != audioSource.ID)
        {
            DisposeSoundPlayer(soundPlayer);
            soundPlayer = CreateSoundPlayer(entity, audioStream);
        }

        //  Ensure the SoundPlayer is attached to the correct mixer
        if (soundPlayer.Parent == null)
        {
            playbackDevice.MasterMixer.AddComponent(soundPlayer);
        }
        else if (soundPlayer.Parent != playbackDevice.MasterMixer)
        {
            soundPlayer.Parent.RemoveComponent(soundPlayer);
            playbackDevice.MasterMixer.AddComponent(soundPlayer);
        }
        
        //  Mix volume
        soundPlayer.Volume = channel.Volume * audioPlayer.Volume;
        
        //  Manage looping
        soundPlayer.IsLooping = audioPlayer.Loop;

        //  Manage pitch
        soundPlayer.PlaybackSpeed = audioPlayer.Pitch;
        
        //  Manage state
        switch (audioPlayer.State)
        {
            case PlayerState.Stop when soundPlayer.State != PlaybackState.Stopped:
                soundPlayer.Stop();
                break;
            case PlayerState.Pause when soundPlayer.State != PlaybackState.Paused:
                soundPlayer.Pause();
                break;
            case PlayerState.Play:
                //  When the component is starting to play,
                //  and the SoundPlayer isn't paused,
                //  reset the SoundPlayer
                if (soundPlayer.State != PlaybackState.Paused)
                {
                    soundPlayer.Stop();
                }
                
                soundPlayer.Play();
                break;
        }
        
        //  Mark completed PlayOnce players for cleanup
        if (!audioPlayer.Loop && audioPlayer.PlayOnce && !soundPlayer.Enabled && audioPlayer.State == PlayerState.Playing)
        {
            store.AddOrUpdate(entity, new CleanupAudioPlayer());
        }

        //  Ensure player state matches the SoundPlayer
        audioPlayer.State = soundPlayer.State switch
        {
            PlaybackState.Stopped => PlayerState.Stop,
            PlaybackState.Playing => PlayerState.Playing,
            PlaybackState.Paused => PlayerState.Pause,
            _ => audioPlayer.State,
        };
    }

    private SoundPlayer CreateSoundPlayer(int entity, AudioStream audioStream)
    {
        var provider = new StreamDataProvider(_engine, _format, audioStream.CreateStream());
        var soundPlayer = new SoundPlayer(_engine, _format, provider);
        _soundPlayers[entity] = soundPlayer;
        return soundPlayer;
    }

    private void DisposeSoundPlayer(SoundPlayer soundPlayer)
    {
        _soundPlayerSources.Remove(soundPlayer);
        
        soundPlayer.Parent?.RemoveComponent(soundPlayer);
        soundPlayer.DataProvider.Dispose();
        soundPlayer.Dispose();
    }
}