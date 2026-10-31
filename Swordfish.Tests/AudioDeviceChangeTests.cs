#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Structs;
using Swordfish.Audio;
using Swordfish.Audio.SoundFlow;
using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Settings;
using Xunit;
using AudioStreamResult = Swordfish.Library.Util.Result<Swordfish.Audio.AudioStream>;

namespace Swordfish.Tests;

public class AudioDeviceChangeTests
{
    [Fact]
    public void ScanIntervalElapses_InitializesConnectedDevice()
    {
        FakeAudioEngine engine = new();
        using AudioSystem system = CreateSystem(engine);
        DataStore store = new();

        Assert.Empty(engine.ActiveDevices);

        engine.Devices.Add(Device("Speakers", isDefault: true));
        system.Tick(1f, store);
        Assert.Empty(engine.ActiveDevices);

        system.Tick(1f, store);
        FakePlaybackDevice device = Assert.Single(engine.ActiveDevices);
        Assert.Equal("Speakers", device.Info?.Name);
    }

    [Fact]
    public void ConnectedDeviceDisappears_DisposesDevice()
    {
        FakeAudioEngine engine = new();
        engine.Devices.Add(Device("Speakers", isDefault: true));
        using AudioSystem system = CreateSystem(engine);
        FakePlaybackDevice device = Assert.Single(engine.ActiveDevices);

        engine.Devices.Clear();
        system.Tick(2f, new DataStore());

        Assert.True(device.IsDisposed);
    }

    [Fact]
    public void FindDefaultName_NoDevices_ReturnsNull()
    {
        string? name = PlaybackDeviceSet.FindDefaultName([]);

        Assert.Null(name);
    }

    [Fact]
    public void FindDefaultName_ReturnsMarkedDevice()
    {
        DeviceInfo[] devices =
        [
            Device("Speakers", isDefault: false),
            Device("Headphones", isDefault: true),
        ];

        string? name = PlaybackDeviceSet.FindDefaultName(devices);

        Assert.Equal("Headphones", name);
    }

    [Fact]
    public void FindDefaultName_DefaultChanges_ReturnsNewDefault()
    {
        DeviceInfo[] before =
        [
            Device("Speakers", isDefault: true),
            Device("Headphones", isDefault: false),
        ];
        DeviceInfo[] after =
        [
            Device("Speakers", isDefault: false),
            Device("Headphones", isDefault: true),
        ];

        Assert.Equal("Speakers", PlaybackDeviceSet.FindDefaultName(before));
        Assert.Equal("Headphones", PlaybackDeviceSet.FindDefaultName(after));
    }

    [Fact]
    public void FindRemoved_ReturnsAbsentNames()
    {
        DeviceInfo[] discovered =
        [
            Device("Speakers", isDefault: true),
        ];
        List<string> removed = [];

        PlaybackDeviceSet.FindRemoved(["Speakers", "Headphones"], discovered, removed);

        Assert.Equal(["Headphones"], removed);
    }

    [Fact]
    public void FindRemoved_EmptyEnumeration_ReturnsAllNames()
    {
        List<string> removed = [];

        PlaybackDeviceSet.FindRemoved(["Speakers", "Headphones"], [], removed);

        Assert.Equal(["Speakers", "Headphones"], removed);
    }

    [Fact]
    public void Contains_ReportsWhetherNameExists()
    {
        DeviceInfo[] devices =
        [
            Device("Speakers", isDefault: true),
        ];

        Assert.True(PlaybackDeviceSet.Contains(devices, "Speakers"));
        Assert.False(PlaybackDeviceSet.Contains(devices, "Headphones"));
    }

    private static AudioSystem CreateSystem(FakeAudioEngine engine)
    {
        return new AudioSystem(
            NullLogger<AudioSystem>.Instance,
            engine,
            new AudioSettings(),
            new EmptyAudioStreamDatabase()
        );
    }

    private static DeviceInfo Device(string name, bool isDefault)
    {
        return new DeviceInfo
        {
            Name = name,
            IsDefault = isDefault,
        };
    }

    private sealed class FakeAudioEngine : AudioEngine
    {
        public List<DeviceInfo> Devices { get; } = [];

        public List<FakePlaybackDevice> ActiveDevices { get; } = [];

        public override void UpdateAudioDevicesInfo()
        {
            PlaybackDevices = Devices.ToArray();
        }

        public override AudioPlaybackDevice InitializePlaybackDevice(DeviceInfo? deviceInfo, AudioFormat format, DeviceConfig? config = null)
        {
            FakePlaybackDevice device = new(this, format, deviceInfo, config ?? new FakeDeviceConfig());
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

        public override AudioCaptureDevice InitializeCaptureDevice(DeviceInfo? deviceInfo, AudioFormat format, DeviceConfig? config = null)
        {
            throw new NotSupportedException();
        }

        public override FullDuplexDevice InitializeFullDuplexDevice(DeviceInfo? playbackDeviceInfo, DeviceInfo? captureDeviceInfo, AudioFormat format, DeviceConfig? config = null)
        {
            throw new NotSupportedException();
        }

        public override AudioCaptureDevice InitializeLoopbackDevice(AudioFormat format, DeviceConfig? config = null)
        {
            throw new NotSupportedException();
        }

        public override AudioPlaybackDevice SwitchDevice(AudioPlaybackDevice oldDevice, DeviceInfo newDeviceInfo, DeviceConfig? config = null)
        {
            throw new NotSupportedException();
        }

        public override AudioCaptureDevice SwitchDevice(AudioCaptureDevice oldDevice, DeviceInfo newDeviceInfo, DeviceConfig? config = null)
        {
            throw new NotSupportedException();
        }

        public override FullDuplexDevice SwitchDevice(FullDuplexDevice oldDevice, DeviceInfo? newPlaybackInfo, DeviceInfo? newCaptureInfo, DeviceConfig? config = null)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakePlaybackDevice : AudioPlaybackDevice
    {
        public FakePlaybackDevice(AudioEngine engine, AudioFormat format, DeviceInfo? info, DeviceConfig config) : base(engine, format, config)
        {
            Info = info;
        }

        public override void Start()
        {
            IsRunning = true;
        }

        public override void Stop()
        {
            IsRunning = false;
        }

        public override void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class FakeDeviceConfig : DeviceConfig;

    private sealed class EmptyAudioStreamDatabase : IAssetDatabase<AudioStream>
    {
        public AudioStreamResult Get(string id)
        {
            return AudioStreamResult.FromFailure("Not found.");
        }
    }
}
