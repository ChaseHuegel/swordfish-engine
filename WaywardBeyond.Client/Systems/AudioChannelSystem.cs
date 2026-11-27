using System;
using System.Collections.Concurrent;
using Krill;
using Swordfish.Audio;
using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Configuration;

namespace WaywardBeyond.Client.Systems;

/// <summary>Maintains client audio channel volumes and dispatches queued audio plays on the ECS thread.</summary>
internal sealed class AudioChannelSystem : IEntitySystem, IDisposable
{
    private readonly VolumeSettings _volumeSettings;
    private readonly IWindowContext _windowContext;
    private readonly Func<bool> _isPaused;

    private int? _masterChannel;
    private int? _effectsChannel;
    private int? _interfaceChannel;
    private int? _musicChannel;

    private float _lastMasterVolume;
    private float _lastEffectsVolume;
    private float _lastInterfaceVolume;
    private float _lastMusicVolume;

    private volatile bool _isWindowFocused = true;

    private readonly ConcurrentDictionary<string, Uuid> _channelEntities = [];

    //  Pending audio plays enqueued off the ECS thread (e.g. UI/menu sounds) and allocated into the store
    //  here on the ECS thread, so store mutations never happen off-thread.
    private readonly ConcurrentQueue<AudioPlay> _pendingPlays = new();

    /// <summary>Creates the audio channel system bound to the volume settings and window focus events.</summary>
    public AudioChannelSystem(in VolumeSettings volumeSettings, in IWindowContext windowContext)
        : this(volumeSettings, windowContext, IsGamePaused)
    {
    }

    internal AudioChannelSystem(in VolumeSettings volumeSettings, in IWindowContext windowContext, Func<bool> isPaused)
    {
        _volumeSettings = volumeSettings ?? throw new ArgumentNullException(nameof(volumeSettings));
        _windowContext = windowContext ?? throw new ArgumentNullException(nameof(windowContext));
        _isPaused = isPaused ?? throw new ArgumentNullException(nameof(isPaused));

        _windowContext.Focused += OnWindowFocused;
        _windowContext.Unfocused += OnWindowUnfocused;
    }

    /// <summary>Gets the entity UUID for a named audio channel if initialized.</summary>
    public bool TryGetChannelEntity(string name, out Uuid channel)
    {
        return _channelEntities.TryGetValue(name, out channel);
    }

    /// <summary>Queues an audio playback entity to be allocated on the ECS thread.</summary>
    public void EnqueuePlay(in AudioSource audioSource, in AudioPlayer audioPlayer)
    {
        _pendingPlays.Enqueue(new AudioPlay(audioSource, audioPlayer));
    }

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        float attenuation = GetAttenuation();
        float masterVolume = _volumeSettings.Master.Get() * attenuation;
        float effectsVolume = _volumeSettings.Effects.Get() * masterVolume;
        float interfaceVolume = _volumeSettings.Interface.Get() * masterVolume;
        float musicVolume = _volumeSettings.Music.Get() * masterVolume;

        UpdateChannel(store, "master", masterVolume, ref _masterChannel, ref _lastMasterVolume);
        UpdateChannel(store, "effects", effectsVolume, ref _effectsChannel, ref _lastEffectsVolume);
        UpdateChannel(store, "interface", interfaceVolume, ref _interfaceChannel, ref _lastInterfaceVolume);
        UpdateChannel(store, "music", musicVolume, ref _musicChannel, ref _lastMusicVolume);

        //  Allocate queued audio plays on the ECS thread
        while (_pendingPlays.TryDequeue(out AudioPlay play))
        {
            store.Alloc(play.AudioSource, play.AudioPlayer);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _windowContext.Focused -= OnWindowFocused;
        _windowContext.Unfocused -= OnWindowUnfocused;
    }

    private float GetAttenuation()
    {
        float attenuation = 1f;
        if (!_isWindowFocused)
        {
            attenuation = MathF.Min(attenuation, Math.Clamp(_volumeSettings.Background.Get(), 0f, 1f));
        }

        if (_isPaused())
        {
            attenuation = MathF.Min(attenuation, Math.Clamp(_volumeSettings.Paused.Get(), 0f, 1f));
        }

        return attenuation;
    }

    private void UpdateChannel(DataStore store, string name, float volume, ref int? entity, ref float lastVolume)
    {
        if (entity == null)
        {
            entity = store.Alloc(new IdentifierComponent(name: name, tag: "audio"), new AudioChannel(volume));
            _channelEntities[name] = store.GetUuid(entity.Value);
            lastVolume = volume;
            return;
        }

        if (lastVolume != volume)
        {
            store.AddOrUpdate(entity.Value, new AudioChannel(volume));
            lastVolume = volume;
        }
    }

    private void OnWindowFocused()
    {
        _isWindowFocused = true;
    }

    private void OnWindowUnfocused()
    {
        _isWindowFocused = false;
    }

    private static bool IsGamePaused()
    {
        return WaywardBeyond.GameState == GameState.Paused;
    }

    private readonly struct AudioPlay(in AudioSource audioSource, in AudioPlayer audioPlayer)
    {
        public readonly AudioSource AudioSource = audioSource;
        public readonly AudioPlayer AudioPlayer = audioPlayer;
    }
}