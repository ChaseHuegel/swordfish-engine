using Microsoft.Extensions.Logging;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;
using Swordfish.Library.Collections;
using Swordfish.Settings;
using Result = Swordfish.Library.Util.Result;

namespace Swordfish.Audio.SoundFlow;

internal class AudioService : IDisposable, IAudioService
{
    private readonly AudioEngine _engine;
    private readonly AudioFormat _format;
    private readonly AudioPlaybackDevice? _playbackDevice;
    private readonly IAssetDatabase<AudioSource> _audioSourceDatabase;

    public AudioService(ILogger<AudioService> logger, AudioEngine engine, AudioSettings audioSettings, IAssetDatabase<AudioSource> audioSourceDatabase)
    {
        _engine = engine;
        _audioSourceDatabase = audioSourceDatabase;
        
        _format = new AudioFormat
        {
            Format = SampleFormat.F32,
            SampleRate = audioSettings.Playback.SampleRate,
            Channels = audioSettings.Playback.Channels,
        };
        
        DeviceInfo defaultPlaybackDevice = _engine.PlaybackDevices.FirstOrDefault(device => device.IsDefault);
        if (defaultPlaybackDevice.Id != IntPtr.Zero)
        {
            _playbackDevice = _engine.InitializePlaybackDevice(defaultPlaybackDevice, _format);
        }
        else
        {
            _playbackDevice = null;
            logger.LogWarning("No default playback device found.");
        }
    }
    
    public void Dispose()
    {
        _engine.Dispose();
        _playbackDevice?.Dispose();
    }

    public Result Play(string id, float volume = 1, bool block = false)
    {
        Library.Util.Result<AudioSource> audioSource = _audioSourceDatabase.Get(id);
        if (!audioSource.Success)
        {
            return new Result(success: false, audioSource.Message, audioSource.Exception);
        }

        return Play(audioSource, volume, block);
    }
    
    public Result Play(AudioSource audioSource, float volume = 1, bool block = false)
    {
        AudioPlaybackDevice? playbackDevice = _playbackDevice;
        return playbackDevice != null ? Play(playbackDevice, audioSource, volume, block) : Result.FromFailure("No playback device is selected.");
    }
    
    private Result Play(AudioPlaybackDevice playbackDevice, AudioSource audioSource, float volume, bool block)
    {
        if (!playbackDevice.IsRunning)
        {
            playbackDevice.Start();
        }
        
        var provider = new StreamDataProvider(_engine, _format, audioSource.CreateStream());
        var player = new SoundPlayer(_engine, _format, provider);
        player.Volume = volume;
        
        EventWaitHandle? waitHandle = block ? new EventWaitHandle(false, EventResetMode.ManualReset) : null;
        player.PlaybackEnded += PlayerOnPlaybackEnded;
        
        playbackDevice.MasterMixer.AddComponent(player);
        player.Play();
        
        waitHandle?.WaitOne();

        void PlayerOnPlaybackEnded(object? sender, EventArgs e)
        {
            playbackDevice.MasterMixer.RemoveComponent(player);
            player.Dispose();
            provider.Dispose();
            waitHandle?.Set();
        }
        
        return Result.FromSuccess();
    }
}