#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;
using Swordfish.Audio;
using Swordfish.Audio.SoundFlow;
using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Physics;
using Swordfish.Settings;
using Xunit;
using AudioStreamResult = Swordfish.Library.Util.Result<Swordfish.Audio.AudioStream>;

namespace Swordfish.Tests;

/// <summary>Covers 3D spatial audio attenuation, panning, Doppler pitch shift, and AudioSystem integration.</summary>
public class SpatialAudioTests
{
    private static readonly SpatialAudioConfig _defaultConfig = new(
        SpeedOfSound: 343f,
        DopplerScale: 1f,
        MinDopplerPitch: 0.5f,
        MaxDopplerPitch: 2f
    );

    [Fact]
    public void SpatialAudio_Defaults_UseExpectedSpatialRange()
    {
        var spatial = new SpatialAudio();

        Assert.Equal(4f, spatial.MinDistance);
        Assert.Equal(64f, spatial.MaxDistance);
        Assert.Equal(1f, spatial.RolloffFactor);
        Assert.Equal(AudioRolloffMode.Inverse, spatial.RolloffMode);
        Assert.Equal(1f, spatial.DopplerFactor);
        Assert.Equal(1f, spatial.SpatialBlend);
    }

    [Theory]
    [InlineData(AudioRolloffMode.Inverse)]
    [InlineData(AudioRolloffMode.Linear)]
    [InlineData(AudioRolloffMode.Exponential)]
    public void ComputeAttenuation_WithinMinDistance_ReturnsFullVolume(AudioRolloffMode mode)
    {
        Assert.Equal(1f, SpatialAudioSolver.ComputeAttenuation(0f, 2f, 32f, 1f, mode));
        Assert.Equal(1f, SpatialAudioSolver.ComputeAttenuation(1f, 2f, 32f, 1f, mode));
        Assert.Equal(1f, SpatialAudioSolver.ComputeAttenuation(2f, 2f, 32f, 1f, mode));
    }

    [Theory]
    [InlineData(AudioRolloffMode.Inverse)]
    [InlineData(AudioRolloffMode.Linear)]
    [InlineData(AudioRolloffMode.Exponential)]
    public void ComputeAttenuation_AtOrBeyondMaxDistance_ReturnsSilence(AudioRolloffMode mode)
    {
        Assert.Equal(0f, SpatialAudioSolver.ComputeAttenuation(32f, 2f, 32f, 1f, mode));
        Assert.Equal(0f, SpatialAudioSolver.ComputeAttenuation(100f, 2f, 32f, 1f, mode));
    }

    [Theory]
    [InlineData(AudioRolloffMode.Inverse)]
    [InlineData(AudioRolloffMode.Linear)]
    [InlineData(AudioRolloffMode.Exponential)]
    public void ComputeAttenuation_DecreasesMonotonicallyAcrossRange(AudioRolloffMode mode)
    {
        float near = SpatialAudioSolver.ComputeAttenuation(4f, 1f, 32f, 1f, mode);
        float mid = SpatialAudioSolver.ComputeAttenuation(12f, 1f, 32f, 1f, mode);
        float far = SpatialAudioSolver.ComputeAttenuation(24f, 1f, 32f, 1f, mode);

        Assert.InRange(near, 0f, 1f);
        Assert.InRange(mid, 0f, 1f);
        Assert.InRange(far, 0f, 1f);
        Assert.True(near > mid);
        Assert.True(mid > far);
        Assert.True(far > 0f);
    }

    [Fact]
    public void ComputeAttenuation_LinearMidpoint_IsHalf()
    {
        float gain = SpatialAudioSolver.ComputeAttenuation(11f, 1f, 21f, 1f, AudioRolloffMode.Linear);

        Assert.Equal(0.5f, gain, precision: 4);
    }

    [Fact]
    public void ComputeAttenuation_ZeroRolloff_KeepsFullVolumeInsideMaxDistance()
    {
        Assert.Equal(1f, SpatialAudioSolver.ComputeAttenuation(15f, 1f, 32f, 0f, AudioRolloffMode.Inverse));
        Assert.Equal(1f, SpatialAudioSolver.ComputeAttenuation(15f, 1f, 32f, 0f, AudioRolloffMode.Linear));
        Assert.Equal(1f, SpatialAudioSolver.ComputeAttenuation(15f, 1f, 32f, 0f, AudioRolloffMode.Exponential));
    }

    [Fact]
    public void ComputeAttenuation_UnboundedInverse_AttenuatesWithoutCutoff()
    {
        float gain = SpatialAudioSolver.ComputeAttenuation(4f, 1f, 0f, 1f, AudioRolloffMode.Inverse);

        Assert.Equal(0.25f, gain, precision: 4);
    }

    [Fact]
    public void ComputePan_MatchesListenerRightAxis()
    {
        Vector3 right = Vector3.UnitX;

        Assert.Equal(1f, SpatialAudioSolver.ComputePan(Vector3.UnitX, right), precision: 4);
        Assert.Equal(0f, SpatialAudioSolver.ComputePan(-Vector3.UnitX, right), precision: 4);
        Assert.Equal(0.5f, SpatialAudioSolver.ComputePan(-Vector3.UnitZ, right), precision: 4);
        Assert.Equal(0.5f, SpatialAudioSolver.ComputePan(Vector3.UnitZ, right), precision: 4);
        Assert.Equal(0.5f, SpatialAudioSolver.ComputePan(Vector3.UnitY, right), precision: 4);
    }

    [Fact]
    public void Solve_RotatedListener_PansRelativeToOrientation()
    {
        //  Rotate 90 degrees left around Y so local right (+X) now points along -Z.
        Quaternion orientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f);
        Vector3 right = Vector3.Normalize(Vector3.Transform(Vector3.UnitX, orientation));
        var listener = new SpatialListenerState(Vector3.Zero, right, Vector3.Zero);
        var spatial = new SpatialAudio(minDistance: 1f, maxDistance: 64f);

        SpatialAudioResult onRight = SpatialAudioSolver.Solve(in listener, new Vector3(0f, 0f, -10f), Vector3.Zero, in spatial, in _defaultConfig);
        SpatialAudioResult onLeft = SpatialAudioSolver.Solve(in listener, new Vector3(0f, 0f, 10f), Vector3.Zero, in spatial, in _defaultConfig);

        Assert.Equal(1f, onRight.Pan, precision: 4);
        Assert.Equal(0f, onLeft.Pan, precision: 4);
    }

    [Fact]
    public void ComputeDopplerPitch_ApproachingRaisesAndRecedingLowers()
    {
        Vector3 dirToSource = Vector3.UnitX;

        float sourceApproaching = SpatialAudioSolver.ComputeDopplerPitch(dirToSource, Vector3.Zero, new Vector3(-34.3f, 0f, 0f), 1f, in _defaultConfig);
        float sourceReceding = SpatialAudioSolver.ComputeDopplerPitch(dirToSource, Vector3.Zero, new Vector3(34.3f, 0f, 0f), 1f, in _defaultConfig);
        float listenerApproaching = SpatialAudioSolver.ComputeDopplerPitch(dirToSource, new Vector3(34.3f, 0f, 0f), Vector3.Zero, 1f, in _defaultConfig);
        float listenerReceding = SpatialAudioSolver.ComputeDopplerPitch(dirToSource, new Vector3(-34.3f, 0f, 0f), Vector3.Zero, 1f, in _defaultConfig);
        float coMoving = SpatialAudioSolver.ComputeDopplerPitch(dirToSource, new Vector3(50f, 0f, 0f), new Vector3(50f, 0f, 0f), 1f, in _defaultConfig);

        Assert.True(sourceApproaching > 1.05f);
        Assert.True(sourceReceding < 0.95f);
        Assert.True(listenerApproaching > 1.05f);
        Assert.True(listenerReceding < 0.95f);
        Assert.Equal(1f, coMoving, precision: 4);
    }

    [Fact]
    public void ComputeDopplerPitch_SupersonicSpeeds_ClampWithinConfiguredBounds()
    {
        Vector3 dirToSource = Vector3.UnitX;

        float approaching = SpatialAudioSolver.ComputeDopplerPitch(dirToSource, Vector3.Zero, new Vector3(-5000f, 0f, 0f), 1f, in _defaultConfig);
        float receding = SpatialAudioSolver.ComputeDopplerPitch(dirToSource, new Vector3(-5000f, 0f, 0f), Vector3.Zero, 1f, in _defaultConfig);

        Assert.Equal(_defaultConfig.MaxDopplerPitch, approaching, precision: 4);
        Assert.Equal(_defaultConfig.MinDopplerPitch, receding, precision: 4);
    }

    [Fact]
    public void ComputeDopplerPitch_ZeroScaleOrFactor_ReturnsUnshifted()
    {
        Vector3 dirToSource = Vector3.UnitX;
        SpatialAudioConfig disabledConfig = _defaultConfig with { DopplerScale = 0f };

        Assert.Equal(1f, SpatialAudioSolver.ComputeDopplerPitch(dirToSource, Vector3.Zero, new Vector3(-100f, 0f, 0f), 0f, in _defaultConfig));
        Assert.Equal(1f, SpatialAudioSolver.ComputeDopplerPitch(dirToSource, Vector3.Zero, new Vector3(-100f, 0f, 0f), 1f, in disabledConfig));
    }

    [Fact]
    public void Solve_SpatialBlend_InterpolatesBetween2DAnd3D()
    {
        var listener = new SpatialListenerState(Vector3.Zero, Vector3.UnitX, Vector3.Zero);
        var full2D = new SpatialAudio(minDistance: 1f, maxDistance: 11f, rolloffFactor: 1f, rolloffMode: AudioRolloffMode.Linear, dopplerFactor: 1f, spatialBlend: 0f);
        var halfBlend = full2D with { SpatialBlend = 0.5f };

        SpatialAudioResult twoD = SpatialAudioSolver.Solve(in listener, new Vector3(11f, 0f, 0f), new Vector3(-100f, 0f, 0f), in full2D, in _defaultConfig);
        SpatialAudioResult blended = SpatialAudioSolver.Solve(in listener, new Vector3(11f, 0f, 0f), Vector3.Zero, in halfBlend, in _defaultConfig);

        Assert.Equal(1f, twoD.VolumeMultiplier, precision: 4);
        Assert.Equal(0.5f, twoD.Pan, precision: 4);
        Assert.Equal(1f, twoD.PitchMultiplier, precision: 4);

        Assert.Equal(0.5f, blended.VolumeMultiplier, precision: 4);
        Assert.Equal(0.75f, blended.Pan, precision: 4);
    }

    [Fact]
    public void AudioSystem_SpatialPlayer_AppliesAttenuationAndPanRelativeToListener()
    {
        using TestHarness harness = CreateHarness();
        int channelEntity = harness.Store.Alloc(new AudioChannel(volume: 0.8f));
        Uuid channelUuid = harness.Store.GetUuid(channelEntity);

        harness.Store.Alloc(new AudioListener(), new TransformComponent(Vector3.Zero, Quaternion.Identity));
        int soundEntity = harness.Store.Alloc(
            new AudioSource("clip"),
            new AudioPlayer(channelUuid) { Volume = 1f, Pitch = 1f, State = PlayerState.Play },
            new TransformComponent(new Vector3(11f, 0f, 0f)),
            new SpatialAudio(minDistance: 1f, maxDistance: 21f, rolloffMode: AudioRolloffMode.Linear)
        );

        harness.System.Tick(0.016f, harness.Store);

        SoundPlayer player = harness.SinglePlayer();
        Assert.Equal(0.4f, player.Volume, precision: 3);
        Assert.Equal(1f, player.Pan, precision: 3);
        Assert.Equal(1f, player.PlaybackSpeed, precision: 3);

        //  Move the source to the left and verify pan flips to 0.
        harness.Store.AddOrUpdate(soundEntity, new TransformComponent(new Vector3(-11f, 0f, 0f)));
        harness.System.Tick(0.016f, harness.Store);

        Assert.Equal(0f, player.Pan, precision: 3);
    }

    [Fact]
    public void AudioSystem_NonSpatialPlayer_RemainsCenteredAndUnattenuated()
    {
        using TestHarness harness = CreateHarness();
        int channelEntity = harness.Store.Alloc(new AudioChannel(volume: 0.5f));
        Uuid channelUuid = harness.Store.GetUuid(channelEntity);

        harness.Store.Alloc(new AudioListener(), new TransformComponent(new Vector3(100f, 0f, 0f), Quaternion.Identity));
        harness.Store.Alloc(
            new AudioSource("clip"),
            new AudioPlayer(channelUuid) { Volume = 0.6f, Pitch = 1.2f, State = PlayerState.Play },
            new TransformComponent(Vector3.Zero)
        );

        harness.System.Tick(0.016f, harness.Store);

        SoundPlayer player = harness.SinglePlayer();
        Assert.Equal(0.3f, player.Volume, precision: 3);
        Assert.Equal(0.5f, player.Pan, precision: 3);
        Assert.Equal(1.2f, player.PlaybackSpeed, precision: 3);
    }

    [Fact]
    public void AudioSystem_DopplerUsesPhysicsVelocityAndParentPhysicsForChildListener()
    {
        using TestHarness harness = CreateHarness();
        int channelEntity = harness.Store.Alloc(new AudioChannel(volume: 1f));
        Uuid channelUuid = harness.Store.GetUuid(channelEntity);

        int parentEntity = harness.Store.Alloc(
            new TransformComponent(Vector3.Zero, Quaternion.Identity),
            new PhysicsComponent(layer: 0, BodyType.Dynamic, CollisionDetection.Discrete, velocity: new Vector3(34.3f, 0f, 0f), torque: Vector3.Zero)
        );
        harness.Store.Alloc(
            new AudioListener(),
            new ChildComponent(new Entity(parentEntity, harness.Store)),
            new TransformComponent(Vector3.Zero, Quaternion.Identity)
        );
        harness.Store.Alloc(
            new AudioSource("clip"),
            new AudioPlayer(channelUuid) { Volume = 1f, Pitch = 1f, State = PlayerState.Play },
            new TransformComponent(new Vector3(10f, 0f, 0f)),
            new SpatialAudio(minDistance: 1f, maxDistance: 64f)
        );

        harness.System.Tick(0.016f, harness.Store);

        SoundPlayer player = harness.SinglePlayer();
        Assert.Equal(1.1f, player.PlaybackSpeed, precision: 3);
    }

    [Fact]
    public void AudioSystem_DopplerDerivesTransformVelocityAcrossTicksWithoutFirstTickSpike()
    {
        using TestHarness harness = CreateHarness();
        int channelEntity = harness.Store.Alloc(new AudioChannel(volume: 1f));
        Uuid channelUuid = harness.Store.GetUuid(channelEntity);

        harness.Store.Alloc(new AudioListener(), new TransformComponent(Vector3.Zero, Quaternion.Identity));
        int soundEntity = harness.Store.Alloc(
            new AudioSource("clip"),
            new AudioPlayer(channelUuid) { Volume = 1f, Pitch = 1f, State = PlayerState.Play },
            new TransformComponent(new Vector3(50f, 0f, 0f)),
            new SpatialAudio(minDistance: 1f, maxDistance: 200f)
        );

        //  First tick establishes initial position without a teleport Doppler spike.
        harness.System.Tick(0.1f, harness.Store);
        SoundPlayer player = harness.SinglePlayer();
        Assert.Equal(1f, player.PlaybackSpeed, precision: 4);

        //  Move 3.43 units closer in 0.1s -> -34.3 u/s toward listener -> pitch increases.
        harness.Store.AddOrUpdate(soundEntity, new TransformComponent(new Vector3(46.57f, 0f, 0f)));
        harness.System.Tick(0.1f, harness.Store);

        Assert.True(player.PlaybackSpeed > 1.09f);
    }

    [Fact]
    public void AudioSystem_WithoutListener_FallsBackToCentered2DPlayback()
    {
        using TestHarness harness = CreateHarness();
        int channelEntity = harness.Store.Alloc(new AudioChannel(volume: 0.75f));
        Uuid channelUuid = harness.Store.GetUuid(channelEntity);

        harness.Store.Alloc(
            new AudioSource("clip"),
            new AudioPlayer(channelUuid) { Volume = 1f, Pitch = 1f, State = PlayerState.Play },
            new TransformComponent(new Vector3(500f, 0f, 0f)),
            new SpatialAudio(minDistance: 1f, maxDistance: 10f)
        );

        harness.System.Tick(0.016f, harness.Store);

        SoundPlayer player = harness.SinglePlayer();
        Assert.Equal(0.75f, player.Volume, precision: 3);
        Assert.Equal(0.5f, player.Pan, precision: 3);
        Assert.Equal(1f, player.PlaybackSpeed, precision: 3);
    }

    [Fact]
    public void AudioSystem_DopplerScaleSettingZero_DisablesDopplerAtRuntime()
    {
        using TestHarness harness = CreateHarness();
        harness.Settings.Spatial.DopplerScale.Set(0f);

        int channelEntity = harness.Store.Alloc(new AudioChannel(volume: 1f));
        Uuid channelUuid = harness.Store.GetUuid(channelEntity);

        harness.Store.Alloc(new AudioListener(), new TransformComponent(Vector3.Zero, Quaternion.Identity));
        int soundEntity = harness.Store.Alloc(
            new AudioSource("clip"),
            new AudioPlayer(channelUuid) { Volume = 1f, Pitch = 1f, State = PlayerState.Play },
            new TransformComponent(new Vector3(10f, 0f, 0f)),
            new SpatialAudio(minDistance: 1f, maxDistance: 64f)
        );
        harness.Store.AddOrUpdate(soundEntity, new PhysicsComponent(layer: 0, BodyType.Dynamic, CollisionDetection.Discrete, velocity: new Vector3(-50f, 0f, 0f), torque: Vector3.Zero));

        harness.System.Tick(0.016f, harness.Store);

        SoundPlayer player = harness.SinglePlayer();
        Assert.Equal(1f, player.PlaybackSpeed, precision: 4);
    }

    [Fact]
    public void AudioSystem_RemovingSpatialAudio_ResetsPlayerToCentered2D()
    {
        using TestHarness harness = CreateHarness();
        int channelEntity = harness.Store.Alloc(new AudioChannel(volume: 1f));
        Uuid channelUuid = harness.Store.GetUuid(channelEntity);

        harness.Store.Alloc(new AudioListener(), new TransformComponent(Vector3.Zero, Quaternion.Identity));
        int soundEntity = harness.Store.Alloc(
            new AudioSource("clip"),
            new AudioPlayer(channelUuid) { Volume = 0.8f, Pitch = 1f, State = PlayerState.Play },
            new TransformComponent(new Vector3(11f, 0f, 0f)),
            new SpatialAudio(minDistance: 1f, maxDistance: 21f, rolloffMode: AudioRolloffMode.Linear)
        );

        harness.System.Tick(0.016f, harness.Store);
        SoundPlayer player = harness.SinglePlayer();
        Assert.Equal(0.4f, player.Volume, precision: 3);
        Assert.Equal(1f, player.Pan, precision: 3);

        harness.Store.Remove<SpatialAudio>(soundEntity);
        harness.System.Tick(0.016f, harness.Store);

        Assert.Equal(0.8f, player.Volume, precision: 3);
        Assert.Equal(0.5f, player.Pan, precision: 3);
    }

    private static TestHarness CreateHarness()
    {
        var engine = new FakeAudioEngine();
        engine.Devices.Add(new DeviceInfo { Name = "Speakers", IsDefault = true });
        var settings = new AudioSettings();
        var database = new StubAudioStreamDatabase();
        var system = new AudioSystem(NullLogger<AudioSystem>.Instance, engine, settings, database);
        return new TestHarness(engine, settings, database, system, new DataStore());
    }

    private sealed class TestHarness(
        FakeAudioEngine engine,
        AudioSettings settings,
        StubAudioStreamDatabase database,
        AudioSystem system,
        DataStore store
    ) : IDisposable
    {
        public readonly FakeAudioEngine Engine = engine;
        public readonly AudioSettings Settings = settings;
        public readonly AudioSystem System = system;
        public readonly DataStore Store = store;

        public SoundPlayer SinglePlayer()
        {
            FakePlaybackDevice device = Assert.Single(Engine.ActiveDevices);
            SoundComponent component = Assert.Single(device.MasterMixer.Components);
            return Assert.IsType<SoundPlayer>(component);
        }

        public void Dispose()
        {
            System.Dispose();
            database.Dispose();
        }
    }

    private sealed class StubAudioStreamDatabase : IAssetDatabase<AudioStream>, IDisposable
    {
        private readonly AudioStream _stream = new(new MemoryStream(new byte[64]));

        public AudioStreamResult Get(string id) => AudioStreamResult.FromSuccess(_stream);

        public void Dispose() => _stream.Dispose();
    }

    private sealed class FakeAudioEngine : AudioEngine
    {
        public List<DeviceInfo> Devices { get; } = [];
        public List<FakePlaybackDevice> ActiveDevices { get; } = [];

        public FakeAudioEngine()
        {
            RegisterCodecFactory(new FakeCodecFactory());
        }

        public override void UpdateAudioDevicesInfo()
        {
            PlaybackDevices = Devices.ToArray();
        }

        public override AudioPlaybackDevice InitializePlaybackDevice(DeviceInfo? deviceInfo, AudioFormat format, DeviceConfig? config = null)
        {
            var device = new FakePlaybackDevice(this, format, deviceInfo, config ?? new FakeDeviceConfig());
            ActiveDevices.Add(device);
            return device;
        }

        protected override void CleanupBackend()
        {
            foreach (FakePlaybackDevice device in ActiveDevices)
            {
                device.Dispose();
            }

            ActiveDevices.Clear();
        }

        public override AudioCaptureDevice InitializeCaptureDevice(DeviceInfo? deviceInfo, AudioFormat format, DeviceConfig? config = null) => throw new NotSupportedException();
        public override FullDuplexDevice InitializeFullDuplexDevice(DeviceInfo? playbackDeviceInfo, DeviceInfo? captureDeviceInfo, AudioFormat format, DeviceConfig? config = null) => throw new NotSupportedException();
        public override AudioCaptureDevice InitializeLoopbackDevice(AudioFormat format, DeviceConfig? config = null) => throw new NotSupportedException();
        public override AudioPlaybackDevice SwitchDevice(AudioPlaybackDevice oldDevice, DeviceInfo newDeviceInfo, DeviceConfig? config = null) => throw new NotSupportedException();
        public override AudioCaptureDevice SwitchDevice(AudioCaptureDevice oldDevice, DeviceInfo newDeviceInfo, DeviceConfig? config = null) => throw new NotSupportedException();
        public override FullDuplexDevice SwitchDevice(FullDuplexDevice oldDevice, DeviceInfo? newPlaybackInfo, DeviceInfo? newCaptureInfo, DeviceConfig? config = null) => throw new NotSupportedException();
    }

    private sealed class FakePlaybackDevice : AudioPlaybackDevice
    {
        public FakePlaybackDevice(AudioEngine engine, AudioFormat format, DeviceInfo? info, DeviceConfig config) : base(engine, format, config)
        {
            Info = info;
        }

        public override void Start() => IsRunning = true;
        public override void Stop() => IsRunning = false;
        public override void Dispose() => IsDisposed = true;
    }

    private sealed class FakeDeviceConfig : DeviceConfig;

    private sealed class FakeCodecFactory : ICodecFactory
    {
        public string FactoryId => "fake";
        public IReadOnlyCollection<string> SupportedFormatIds { get; } = ["wav"];
        public int Priority => 100;

        public ISoundDecoder? CreateDecoder(Stream stream, string formatId, AudioFormat format) => new FakeSoundDecoder(format);

        public ISoundDecoder? TryCreateDecoder(Stream stream, out AudioFormat detectedFormat, AudioFormat? hintFormat = null)
        {
            detectedFormat = hintFormat ?? new AudioFormat { Format = SampleFormat.F32, Channels = 2, SampleRate = 48000 };
            return new FakeSoundDecoder(detectedFormat);
        }

        public ISoundEncoder CreateEncoder(Stream stream, string formatId, AudioFormat format) => throw new NotSupportedException();
    }

    private sealed class FakeSoundDecoder(AudioFormat format) : ISoundDecoder
    {
        public bool IsDisposed { get; private set; }
        public int Length => format.SampleRate * format.Channels;
        public SampleFormat SampleFormat => format.Format;
        public int Channels => format.Channels;
        public int SampleRate => format.SampleRate;

        public event EventHandler<EventArgs>? EndOfStreamReached
        {
            add { }
            remove { }
        }

        public int Decode(Span<float> samples)
        {
            samples.Clear();
            return samples.Length;
        }

        public bool Seek(int offset) => true;

        public void Dispose() => IsDisposed = true;
    }
}
