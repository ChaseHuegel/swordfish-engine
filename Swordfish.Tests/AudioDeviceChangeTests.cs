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
using Krill;
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

    [Fact]
    public void Service_ListsInitializedDevices()
    {
        FakeAudioEngine engine = new();
        engine.Devices.Add(Device("Speakers", isDefault: true));
        engine.Devices.Add(Device("Headphones", isDefault: false));
        using AudioSystem system = CreateSystem(engine);

        Assert.Equal(new[] { "Headphones", "Speakers" }, system.PlaybackDeviceNames);
        Assert.Equal("Speakers", system.DefaultPlaybackDeviceName);
    }

    [Fact]
    public void Service_OmitsFailedDevices()
    {
        FakeAudioEngine engine = new()
        {
            FailDeviceName = "Broken",
        };
        engine.Devices.Add(Device("Speakers", isDefault: true));
        engine.Devices.Add(Device("Broken", isDefault: false));
        using AudioSystem system = CreateSystem(engine);

        Assert.Equal(new[] { "Speakers" }, system.PlaybackDeviceNames);
    }

    [Fact]
    public void SetPreferredDevice_SetsActiveName()
    {
        (FakeAudioEngine _, AudioSystem system) = CreateTwoDeviceSystem();
        using (system)
        {
            system.SetPreferredPlaybackDevice("Headphones");
            system.Tick(0.1f, new DataStore());

            Assert.Equal("Headphones", system.PreferredPlaybackDeviceName);
            Assert.Equal("Headphones", system.ActivePlaybackDeviceName);
            Assert.False(system.IsPreferredPlaybackDeviceMissing);
        }
    }

    [Fact]
    public void SetPreferredDevice_RaisesChanged()
    {
        (FakeAudioEngine _, AudioSystem system) = CreateTwoDeviceSystem();
        using (system)
        {
            PlaybackDevicesChangedEventArgs? changed = null;
            var changes = 0;
            system.PlaybackDevicesChanged += (_, args) =>
            {
                changed = args;
                changes++;
            };

            system.SetPreferredPlaybackDevice("Headphones");
            system.Tick(0.1f, new DataStore());

            Assert.Equal(1, changes);
            Assert.True(changed.HasValue);
            PlaybackDevicesChangedEventArgs args = changed.GetValueOrDefault();
            Assert.Equal("Headphones", args.ActivePlaybackDeviceName);
            Assert.Equal("Headphones", args.PreferredPlaybackDeviceName);
        }
    }

    [Fact]
    public void ClearPreferredDevice_UsesSystemDefault()
    {
        (FakeAudioEngine _, AudioSystem system) = CreateTwoDeviceSystem();
        using (system)
        {
            system.SetPreferredPlaybackDevice("Headphones");
            system.Tick(0.1f, new DataStore());

            system.SetPreferredPlaybackDevice(null);
            system.Tick(0.1f, new DataStore());

            Assert.Null(system.PreferredPlaybackDeviceName);
            Assert.Equal("Speakers", system.ActivePlaybackDeviceName);
            Assert.False(system.IsPreferredPlaybackDeviceMissing);
        }
    }

    [Fact]
    public void RemovePreferredDevice_FallsBackAndReportsMissing()
    {
        (FakeAudioEngine engine, AudioSystem system) = CreateTwoDeviceSystem();
        using (system)
        {
            system.SetPreferredPlaybackDevice("Headphones");
            system.Tick(0.1f, new DataStore());

            PlaybackDevicesChangedEventArgs? changed = null;
            system.PlaybackDevicesChanged += (_, args) => changed = args;

            engine.Devices.RemoveAll(device => device.Name == "Headphones");
            system.Tick(2f, new DataStore());

            Assert.Equal("Speakers", system.ActivePlaybackDeviceName);
            Assert.True(system.IsPreferredPlaybackDeviceMissing);
            Assert.True(changed.HasValue);
            PlaybackDevicesChangedEventArgs args = changed.GetValueOrDefault();
            Assert.True(args.IsPreferredPlaybackDeviceMissing);
        }
    }

    [Fact]
    public void ReturnPreferredDevice_ResumesPreference()
    {
        (FakeAudioEngine engine, AudioSystem system) = CreateTwoDeviceSystem();
        using (system)
        {
            system.SetPreferredPlaybackDevice("Headphones");
            system.Tick(0.1f, new DataStore());

            engine.Devices.RemoveAll(device => device.Name == "Headphones");
            system.Tick(2f, new DataStore());

            engine.Devices.Add(Device("Headphones", isDefault: false));
            system.Tick(2f, new DataStore());

            Assert.Equal("Headphones", system.ActivePlaybackDeviceName);
            Assert.False(system.IsPreferredPlaybackDeviceMissing);
        }
    }

    [Fact]
    public void UnknownPreferredDevice_IsReportedMissing()
    {
        (FakeAudioEngine _, AudioSystem system) = CreateTwoDeviceSystem();
        using (system)
        {
            system.SetPreferredPlaybackDevice("Ghost");
            system.Tick(0.1f, new DataStore());

            Assert.Equal("Speakers", system.ActivePlaybackDeviceName);
            Assert.True(system.IsPreferredPlaybackDeviceMissing);
        }
    }

    [Fact]
    public void UnchangedScan_DoesNotRaiseChanged()
    {
        (FakeAudioEngine _, AudioSystem system) = CreateTwoDeviceSystem();
        using (system)
        {
            var changes = 0;
            system.PlaybackDevicesChanged += (_, _) => changes++;

            system.Tick(0.1f, new DataStore());
            system.Tick(2f, new DataStore());

            Assert.Equal(0, changes);
        }
    }

    private static (FakeAudioEngine Engine, AudioSystem System) CreateTwoDeviceSystem()
    {
        FakeAudioEngine engine = new();
        engine.Devices.Add(Device("Speakers", isDefault: true));
        engine.Devices.Add(Device("Headphones", isDefault: false));
        return (engine, CreateSystem(engine));
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
        public string? FailDeviceName { get; init; }

        public List<DeviceInfo> Devices { get; } = [];

        public List<FakePlaybackDevice> ActiveDevices { get; } = [];

        public override void UpdateAudioDevicesInfo()
        {
            PlaybackDevices = Devices.ToArray();
        }

        public override AudioPlaybackDevice InitializePlaybackDevice(DeviceInfo? deviceInfo, AudioFormat format, DeviceConfig? config = null)
        {
            if (deviceInfo?.Name == FailDeviceName)
            {
                throw new InvalidOperationException("The device failed to initialize.");
            }

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
