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
    private readonly Dictionary<Uuid, SoundPlayer> _soundPlayers = [];
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
        var cleanupAudioPlayerAction = new CleanupAudioPlayerAction(this);
        store.Query<CleanupAudioPlayer, CleanupAudioPlayerAction>(delta, ref cleanupAudioPlayerAction);

        var tickAudioPlayerAction = new TickAudioPlayerAction(this);
        store.QueryRef<AudioPlayer, AudioSource, TickAudioPlayerAction>(delta, ref tickAudioPlayerAction);
    }

    private SoundPlayer CreateSoundPlayer(Uuid entity, AudioStream audioStream)
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
    
    private readonly struct CleanupAudioPlayerAction(in AudioSystem owner) : IForEach<CleanupAudioPlayer>
    {
        private readonly AudioSystem _owner = owner;

        public void Execute(float delta, DataStore store, int entity, in CleanupAudioPlayer cleanupAudioPlayer)
        {
            Uuid uuid = store.GetUuid(entity);
            if (!_owner._soundPlayers.TryGetValue(uuid, out SoundPlayer? soundPlayer))
            {
                return;
            }

            _owner.DisposeSoundPlayer(soundPlayer);
            _owner._soundPlayers.Remove(uuid);
            _owner._soundPlayerSources.Remove(soundPlayer);
            store.Free(entity);
        }
    }

    private readonly struct TickAudioPlayerAction(in AudioSystem owner) : IForEachRef<AudioPlayer, AudioSource>
    {
        private readonly AudioSystem _owner = owner;

        public void Execute(float delta, DataStore store, int entity, ref Ref<AudioPlayer> audioPlayer, ref Ref<AudioSource> audioSource)
        {
            if (!store.TryGet(audioPlayer.Read.ChannelEntity, out AudioChannel channel))
            {
                //  The player isn't assigned to a valid channel
                return;
            }

            //  Resolve the playback device
            bool useDefaultPlaybackDevice = string.IsNullOrEmpty(channel.PlaybackDevice);
            AudioPlaybackDevice? playbackDevice;
            if (useDefaultPlaybackDevice)
            {
                playbackDevice = _owner._defaultPlaybackDevice;
            }
            else if (!_owner._playbackDevices.TryGetValue(channel.PlaybackDevice!, out playbackDevice))
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

            Library.Util.Result<AudioStream> audioStream = _owner._audioStreamDatabase.Get(audioSource.Read.ID);
            if (!audioStream.Success)
            {
                //  The audio source isn't assigned to a valid stream
                return;
            }
            
            Uuid uuid = store.GetUuid(entity);

            //  Ensure there is an underlying SoundPlayer
            if (!_owner._soundPlayers.TryGetValue(uuid, out SoundPlayer? soundPlayer))
            {
                soundPlayer = _owner.CreateSoundPlayer(uuid, audioStream);
                _owner._soundPlayerSources[soundPlayer] = audioSource.Read.ID;
            }

            //  Recreate the SoundPlayer if the AudioSource has changed
            if (_owner._soundPlayerSources.TryGetValue(soundPlayer, out string? previousAudioSource) && previousAudioSource != audioSource.Read.ID)
            {
                _owner.DisposeSoundPlayer(soundPlayer);
                soundPlayer = _owner.CreateSoundPlayer(uuid, audioStream);
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
            soundPlayer.Volume = channel.Volume * audioPlayer.Read.Volume;

            //  Manage looping
            soundPlayer.IsLooping = audioPlayer.Read.Loop;

            //  Manage pitch
            soundPlayer.PlaybackSpeed = audioPlayer.Read.Pitch;

            //  Manage state
            switch (audioPlayer.Read.State)
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
            if (!audioPlayer.Read.Loop && audioPlayer.Read.PlayOnce && !soundPlayer.Enabled && audioPlayer.Read.State == PlayerState.Playing)
            {
                store.AddOrUpdate(entity, new CleanupAudioPlayer());
            }

            //  Ensure player state matches the SoundPlayer
            audioPlayer.Write.State = soundPlayer.State switch
            {
                PlaybackState.Stopped => PlayerState.Stop,
                PlaybackState.Playing => PlayerState.Playing,
                PlaybackState.Paused => PlayerState.Pause,
                _ => audioPlayer.Read.State,
            };
        }
    }
}