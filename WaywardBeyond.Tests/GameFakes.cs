#nullable enable
using System;
using System.Collections.Generic;
using Krill;
using Swordfish.Audio;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Settings;

namespace WaywardBeyond.Tests;

/// <summary>A playback device stub whose device list a test can drive.</summary>
internal sealed class FakePlaybackDeviceService : IPlaybackDeviceService
{
    public List<string> Devices { get; } = [];

    public string? DefaultPlaybackDeviceName { get; set; }

    public string? PreferredPlaybackDeviceName { get; private set; }

    public string? ActivePlaybackDeviceName { get; private set; }

    public bool IsPreferredPlaybackDeviceMissing { get; private set; }

    public IReadOnlyList<string> PlaybackDeviceNames => Devices;

    public event EventHandler<PlaybackDevicesChangedEventArgs>? PlaybackDevicesChanged;

    /// <inheritdoc/>
    public void SetPreferredPlaybackDevice(string? name)
    {
        PreferredPlaybackDeviceName = string.IsNullOrEmpty(name) ? null : name;
        IsPreferredPlaybackDeviceMissing = PreferredPlaybackDeviceName != null && !Devices.Contains(PreferredPlaybackDeviceName);
        ActivePlaybackDeviceName = !IsPreferredPlaybackDeviceMissing && PreferredPlaybackDeviceName != null
            ? PreferredPlaybackDeviceName
            : DefaultPlaybackDeviceName;
        RaiseChanged();
    }

    /// <summary>Replaces the device list and raises one change event.</summary>
    public void SetDevices(params string[] devices)
    {
        Devices.Clear();
        Devices.AddRange(devices);
        IsPreferredPlaybackDeviceMissing = PreferredPlaybackDeviceName != null && !Devices.Contains(PreferredPlaybackDeviceName);
        ActivePlaybackDeviceName = !IsPreferredPlaybackDeviceMissing && PreferredPlaybackDeviceName != null
            ? PreferredPlaybackDeviceName
            : DefaultPlaybackDeviceName;
        RaiseChanged();
    }

    /// <summary>Raises one change event with the current state.</summary>
    public void RaiseChanged()
    {
        PlaybackDevicesChanged?.Invoke(this, new PlaybackDevicesChangedEventArgs(
            PlaybackDeviceNames,
            DefaultPlaybackDeviceName,
            PreferredPlaybackDeviceName,
            ActivePlaybackDeviceName,
            IsPreferredPlaybackDeviceMissing
        ));
    }
}

/// <summary>A render-context stub backed by a real camera entity, for builders that child to it.</summary>
internal sealed class FakeRenderContext : IRenderContext
{
    public DataBinding<CameraEntity> MainCamera { get; }

    public FakeRenderContext(DataStore store)
    {
        int camera = store.Alloc();
        var transform = new TransformComponent();
        var viewFrustum = new ViewFrustumComponent(0.1f, 1000f, 70);
        store.AddOrUpdate(camera, transform);
        store.AddOrUpdate(camera, viewFrustum);
        MainCamera = new DataBinding<CameraEntity>(new CameraEntity(new Entity(camera, store), viewFrustum, transform));
    }

    RenderScene IRenderContext.GetSceneContext() => default;

    bool IRenderContext.TryDequeueUnbound(out UnboundRenderer unbound)
    {
        unbound = default;
        return false;
    }

    bool IRenderContext.TryDequeueUnboundBatch(out UnboundBatchRenderer unbound)
    {
        unbound = default;
        return false;
    }

    public void Bind(Shader shader) { }

    public void Bind(Texture texture) { }

    public void Bind(Mesh mesh) { }

    public void Bind(MeshRenderer meshRenderer, Uuid entity) { }

    public void Bind(MeshBatchRenderer meshBatchRenderer, Uuid entity) { }

    public void Bind(RectRenderer rectRenderer) { }

    public void Bind(Material material) { }
}
