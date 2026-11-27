using System.Numerics;
using Microsoft.Extensions.Logging;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Settings;

namespace Swordfish.Audio.SoundFlow;

internal sealed class AudioSystem : IEntitySystem, IPlaybackDeviceService, IDisposable
{
    private const float PlaybackDeviceScanInterval = 2f;
    private const float PARAMETER_EPSILON = 1e-5f;
    private const float CENTER_PAN = 0.5f;

    private readonly AudioEngine _engine;
    private readonly ILogger<AudioSystem> _logger;
    private readonly AudioSettings _audioSettings;
    private readonly IAssetDatabase<AudioStream> _audioStreamDatabase;
    
    private readonly AudioFormat _format;
    private readonly Dictionary<Uuid, SoundPlayer> _soundPlayers = [];
    private readonly Dictionary<SoundPlayer, string> _soundPlayerSources = [];
    private readonly Dictionary<Uuid, Vector3> _previousSourcePositions = [];
    private readonly Dictionary<string, AudioPlaybackDevice> _playbackDevices = [];
    private readonly HashSet<string> _failedPlaybackDevices = [];
    
    private AudioPlaybackDevice? _activePlaybackDevice;
    private AudioPlaybackDevice? _defaultPlaybackDevice;
    private string? _defaultPlaybackDeviceName;
    private string? _appliedPreferredPlaybackDeviceName;
    private string? _preferredPlaybackDeviceName;
    private string[] _publishedPlaybackDeviceNames = [];
    private string? _publishedDefaultPlaybackDeviceName;
    private string? _publishedPreferredPlaybackDeviceName;
    private string? _publishedActivePlaybackDeviceName;
    private bool _publishedPreferredPlaybackDeviceMissing;
    private bool? _hasDefaultPlaybackDevice;
    private float _playbackDeviceScanTimer;
    private Uuid? _previousListenerUuid;
    private Vector3 _previousListenerPosition;

    /// <inheritdoc/>
    public IReadOnlyList<string> PlaybackDeviceNames => Volatile.Read(ref _publishedPlaybackDeviceNames);

    /// <inheritdoc/>
    public string? DefaultPlaybackDeviceName => Volatile.Read(ref _publishedDefaultPlaybackDeviceName);

    /// <inheritdoc/>
    public string? PreferredPlaybackDeviceName => Volatile.Read(ref _preferredPlaybackDeviceName);

    /// <inheritdoc/>
    public string? ActivePlaybackDeviceName => Volatile.Read(ref _publishedActivePlaybackDeviceName);

    /// <inheritdoc/>
    public bool IsPreferredPlaybackDeviceMissing => Volatile.Read(ref _publishedPreferredPlaybackDeviceMissing);

    /// <inheritdoc/>
    public event EventHandler<PlaybackDevicesChangedEventArgs>? PlaybackDevicesChanged;
    
    public AudioSystem(ILogger<AudioSystem> logger, AudioEngine engine, AudioSettings audioSettings, IAssetDatabase<AudioStream> audioStreamDatabase)
    {
        _logger = logger;
        _engine = engine;
        _audioSettings = audioSettings;
        _audioStreamDatabase = audioStreamDatabase;
        
        _format = new AudioFormat
        {
            Format = SampleFormat.F32,
            SampleRate = audioSettings.Playback.SampleRate,
            Channels = audioSettings.Playback.Channels,
        };

        RefreshPlaybackDevices();
        ApplyPlaybackDeviceState();
    }

    /// <inheritdoc/>
    public void SetPreferredPlaybackDevice(string? name)
    {
        string? preferredName = string.IsNullOrEmpty(name) ? null : name;
        Volatile.Write(ref _preferredPlaybackDeviceName, preferredName);
    }
    
    public void Dispose()
    {
        PlaybackDevicesChanged = null;
        _engine.Dispose();
        _defaultPlaybackDevice?.Dispose();
    }
    
    public void Tick(float delta, DataStore store)
    {
        _playbackDeviceScanTimer += delta;

        bool scanDue = _playbackDeviceScanTimer >= PlaybackDeviceScanInterval;
        if (scanDue)
        {
            _playbackDeviceScanTimer = 0f;
            RefreshPlaybackDevices();
        }

        string? requestedPreferredDevice = Volatile.Read(ref _preferredPlaybackDeviceName);
        if (scanDue || !string.Equals(requestedPreferredDevice, _appliedPreferredPlaybackDeviceName, StringComparison.Ordinal))
        {
            ApplyPlaybackDeviceState();
        }

        var cleanupAudioPlayerAction = new CleanupAudioPlayerAction(this);
        store.Query<CleanupAudioPlayer, CleanupAudioPlayerAction>(delta, ref cleanupAudioPlayerAction);

        bool hasListener = TryResolveListener(delta, store, out SpatialListenerState listener);
        SpatialAudioConfig spatialConfig = new(
            _audioSettings.Spatial.SpeedOfSound.Get(),
            _audioSettings.Spatial.DopplerScale.Get(),
            _audioSettings.Spatial.MinDopplerPitch.Get(),
            _audioSettings.Spatial.MaxDopplerPitch.Get()
        );

        var tickAudioPlayerAction = new TickAudioPlayerAction(this, hasListener, in listener, in spatialConfig);
        store.QueryRef<AudioPlayer, AudioSource, TickAudioPlayerAction>(delta, ref tickAudioPlayerAction);
    }

    private bool TryResolveListener(float delta, DataStore store, out SpatialListenerState listener)
    {
        var findListenerAction = new FindListenerAction();
        store.Query<AudioListener, TransformComponent, FindListenerAction>(delta, ref findListenerAction);
        if (!findListenerAction.Found)
        {
            _previousListenerUuid = null;
            listener = default;
            return false;
        }

        int entity = findListenerAction.Entity;
        TransformComponent transform = findListenerAction.Transform;
        Uuid uuid = store.GetUuid(entity);

        Vector3 right = transform.GetRight();
        float rightLengthSquared = right.LengthSquared();
        right = rightLengthSquared > PARAMETER_EPSILON * PARAMETER_EPSILON
            ? right / MathF.Sqrt(rightLengthSquared)
            : Vector3.UnitX;

        Vector3 velocity = ResolveVelocity(
            store,
            entity,
            in transform.Position,
            delta,
            hasPreviousPosition: _previousListenerUuid == uuid,
            in _previousListenerPosition
        );

        _previousListenerUuid = uuid;
        _previousListenerPosition = transform.Position;
        listener = new SpatialListenerState(transform.Position, right, velocity);
        return true;
    }

    private static Vector3 ResolveVelocity(
        DataStore store,
        int entity,
        in Vector3 currentPosition,
        float delta,
        bool hasPreviousPosition,
        in Vector3 previousPosition
    ) {
        if (store.TryGet(entity, out PhysicsComponent physics))
        {
            return physics.Velocity;
        }

        if (store.TryGet(entity, out ChildComponent child) && store.TryGet(child.Parent, out PhysicsComponent parentPhysics))
        {
            return parentPhysics.Velocity;
        }

        if (hasPreviousPosition && delta > PARAMETER_EPSILON)
        {
            return (currentPosition - previousPosition) / delta;
        }

        return Vector3.Zero;
    }

    private void RefreshPlaybackDevices()
    {
        try
        {
            _engine.UpdateAudioDevicesInfo();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate playback devices.");
            return;
        }

        DeviceInfo[] playbackDevices = _engine.PlaybackDevices;

        List<string> removedNames = [];
        PlaybackDeviceSet.FindRemoved(_playbackDevices.Keys, playbackDevices, removedNames);
        foreach (string name in removedNames)
        {
            RemovePlaybackDevice(name);
        }

        foreach (DeviceInfo deviceInfo in playbackDevices)
        {
            if (!_playbackDevices.ContainsKey(deviceInfo.Name))
            {
                AddPlaybackDevice(deviceInfo);
            }
        }

        _defaultPlaybackDeviceName = PlaybackDeviceSet.FindDefaultName(playbackDevices);
    }

    private void ApplyPlaybackDeviceState()
    {
        string? preferredName = Volatile.Read(ref _preferredPlaybackDeviceName);
        _appliedPreferredPlaybackDeviceName = preferredName;

        AudioPlaybackDevice? defaultPlaybackDevice = null;
        if (_defaultPlaybackDeviceName != null)
        {
            _playbackDevices.TryGetValue(_defaultPlaybackDeviceName, out defaultPlaybackDevice);
        }

        _defaultPlaybackDevice = defaultPlaybackDevice;

        AudioPlaybackDevice? activePlaybackDevice = null;
        string? activeName = null;
        var isPreferredDeviceMissing = false;

        if (!string.IsNullOrEmpty(preferredName))
        {
            if (_playbackDevices.TryGetValue(preferredName, out AudioPlaybackDevice? preferredPlaybackDevice))
            {
                activePlaybackDevice = preferredPlaybackDevice;
                activeName = preferredName;
            }
            else
            {
                isPreferredDeviceMissing = true;
            }
        }

        if (activePlaybackDevice == null && defaultPlaybackDevice != null)
        {
            activePlaybackDevice = defaultPlaybackDevice;
            activeName = _defaultPlaybackDeviceName;
        }

        _activePlaybackDevice = activePlaybackDevice;

        List<string> deviceNameList = [];
        foreach (string name in _playbackDevices.Keys)
        {
            if (name.Length > 0)
            {
                deviceNameList.Add(name);
            }
        }

        deviceNameList.Sort(StringComparer.Ordinal);
        string[] deviceNames = [..deviceNameList];

        bool hasDefaultPlaybackDevice = defaultPlaybackDevice != null;
        if (_hasDefaultPlaybackDevice != hasDefaultPlaybackDevice)
        {
            _hasDefaultPlaybackDevice = hasDefaultPlaybackDevice;
            if (hasDefaultPlaybackDevice)
            {
                _logger.LogInformation("Default playback device: {device}", _defaultPlaybackDeviceName ?? "unknown");
            }
            else
            {
                _logger.LogWarning("No default playback device found.");
            }
        }

        if (StateEquals(deviceNames, _defaultPlaybackDeviceName, preferredName, activeName, isPreferredDeviceMissing))
        {
            return;
        }

        Volatile.Write(ref _publishedPlaybackDeviceNames, deviceNames);
        Volatile.Write(ref _publishedDefaultPlaybackDeviceName, _defaultPlaybackDeviceName);
        Volatile.Write(ref _publishedPreferredPlaybackDeviceName, preferredName);
        Volatile.Write(ref _publishedActivePlaybackDeviceName, activeName);
        Volatile.Write(ref _publishedPreferredPlaybackDeviceMissing, isPreferredDeviceMissing);

        var args = new PlaybackDevicesChangedEventArgs(
            deviceNames,
            _defaultPlaybackDeviceName,
            preferredName,
            activeName,
            isPreferredDeviceMissing
        );

        RaisePlaybackDevicesChanged(args);
    }

    private bool StateEquals(
        string[] deviceNames,
        string? defaultPlaybackDeviceName,
        string? preferredPlaybackDeviceName,
        string? activePlaybackDeviceName,
        bool isPreferredPlaybackDeviceMissing
    ) {
        if (_publishedPreferredPlaybackDeviceMissing != isPreferredPlaybackDeviceMissing)
        {
            return false;
        }

        if (!string.Equals(_publishedDefaultPlaybackDeviceName, defaultPlaybackDeviceName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(_publishedPreferredPlaybackDeviceName, preferredPlaybackDeviceName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(_publishedActivePlaybackDeviceName, activePlaybackDeviceName, StringComparison.Ordinal))
        {
            return false;
        }

        if (_publishedPlaybackDeviceNames.Length != deviceNames.Length)
        {
            return false;
        }

        for (var i = 0; i < deviceNames.Length; i++)
        {
            if (!string.Equals(_publishedPlaybackDeviceNames[i], deviceNames[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void RaisePlaybackDevicesChanged(in PlaybackDevicesChangedEventArgs args)
    {
        EventHandler<PlaybackDevicesChangedEventArgs>? handlers = PlaybackDevicesChanged;
        if (handlers == null)
        {
            return;
        }

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((EventHandler<PlaybackDevicesChangedEventArgs>)handler)(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "A playback device change handler failed.");
            }
        }
    }

    private void AddPlaybackDevice(DeviceInfo deviceInfo)
    {
        try
        {
            AudioPlaybackDevice playbackDevice = _engine.InitializePlaybackDevice(deviceInfo, _format);
            _playbackDevices[deviceInfo.Name] = playbackDevice;
            _failedPlaybackDevices.Remove(deviceInfo.Name);
            _logger.LogInformation("Playback device initialized: {device}", deviceInfo.Name);
        }
        catch (Exception ex)
        {
            if (_failedPlaybackDevices.Add(deviceInfo.Name))
            {
                _logger.LogWarning(ex, "Failed to initialize playback device: {device}", deviceInfo.Name);
            }
            else
            {
                _logger.LogDebug(ex, "Failed to initialize playback device: {device}", deviceInfo.Name);
            }
        }
    }

    private void RemovePlaybackDevice(string name)
    {
        AudioPlaybackDevice playbackDevice = _playbackDevices[name];
        _playbackDevices.Remove(name);
        _failedPlaybackDevices.Remove(name);

        if (_defaultPlaybackDevice == playbackDevice)
        {
            _defaultPlaybackDevice = null;
        }

        if (_activePlaybackDevice == playbackDevice)
        {
            _activePlaybackDevice = null;
        }

        DetachSoundPlayers(playbackDevice.MasterMixer);
        playbackDevice.Dispose();
        _logger.LogInformation("Playback device removed: {device}", name);
    }

    private void DetachSoundPlayers(Mixer mixer)
    {
        foreach (SoundPlayer soundPlayer in _soundPlayers.Values)
        {
            if (soundPlayer.Parent == mixer)
            {
                mixer.RemoveComponent(soundPlayer);
            }
        }
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
            _owner._previousSourcePositions.Remove(uuid);
            store.Free(entity);
        }
    }

    private struct FindListenerAction : IForEach<AudioListener, TransformComponent>
    {
        public bool Found;
        public int Entity;
        public TransformComponent Transform;

        public void Execute(float delta, DataStore store, int entity, in AudioListener listener, in TransformComponent transform)
        {
            if (Found)
            {
                return;
            }

            Found = true;
            Entity = entity;
            Transform = transform;
        }
    }

    private readonly struct TickAudioPlayerAction(
        in AudioSystem owner,
        bool hasListener,
        in SpatialListenerState listener,
        in SpatialAudioConfig spatialConfig
    ) : IForEachRef<AudioPlayer, AudioSource>
    {
        private readonly AudioSystem _owner = owner;
        private readonly bool _hasListener = hasListener;
        private readonly SpatialListenerState _listener = listener;
        private readonly SpatialAudioConfig _spatialConfig = spatialConfig;

        public void Execute(float delta, DataStore store, int entity, ref Ref<AudioPlayer> audioPlayer, ref Ref<AudioSource> audioSource)
        {
            if (!store.TryGet(audioPlayer.Read.ChannelEntity, out AudioChannel channel))
            {
                //  The player isn't assigned to a valid channel
                return;
            }

            //  Resolve the playback device
            bool useActivePlaybackDevice = string.IsNullOrEmpty(channel.PlaybackDevice);
            AudioPlaybackDevice? playbackDevice;
            if (useActivePlaybackDevice)
            {
                playbackDevice = _owner._activePlaybackDevice;
            }
            else if (!_owner._playbackDevices.TryGetValue(channel.PlaybackDevice!, out playbackDevice))
            {
                playbackDevice = _owner._activePlaybackDevice;
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

            //  Mix volume, pan, and pitch (applying 3D spatialization when present)
            float targetVolume = MathF.Max(0f, channel.Volume * audioPlayer.Read.Volume);
            float targetPan = CENTER_PAN;
            float targetPitch = audioPlayer.Read.Pitch;

            if (_hasListener && store.TryGet(entity, out SpatialAudio spatial) && store.TryGet(entity, out TransformComponent sourceTransform))
            {
                bool hasPreviousPosition = _owner._previousSourcePositions.TryGetValue(uuid, out Vector3 previousPosition);
                Vector3 sourceVelocity = ResolveVelocity(
                    store,
                    entity,
                    in sourceTransform.Position,
                    delta,
                    hasPreviousPosition,
                    in previousPosition
                );
                _owner._previousSourcePositions[uuid] = sourceTransform.Position;

                SpatialAudioResult spatialResult = SpatialAudioSolver.Solve(
                    in _listener,
                    in sourceTransform.Position,
                    in sourceVelocity,
                    in spatial,
                    in _spatialConfig
                );

                targetVolume = MathF.Max(0f, targetVolume * spatialResult.VolumeMultiplier);
                targetPan = spatialResult.Pan;
                targetPitch *= spatialResult.PitchMultiplier;
            }
            else
            {
                _owner._previousSourcePositions.Remove(uuid);
            }

            if (MathF.Abs(soundPlayer.Volume - targetVolume) > PARAMETER_EPSILON)
            {
                soundPlayer.Volume = targetVolume;
            }

            if (MathF.Abs(soundPlayer.Pan - targetPan) > PARAMETER_EPSILON)
            {
                soundPlayer.Pan = targetPan;
            }

            //  Manage looping
            soundPlayer.IsLooping = audioPlayer.Read.Loop;

            //  Manage pitch
            if (MathF.Abs(soundPlayer.PlaybackSpeed - targetPitch) > PARAMETER_EPSILON)
            {
                soundPlayer.PlaybackSpeed = targetPitch;
            }

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