using Krill;
using Swordfish.Audio;
using Swordfish.Tests;
using WaywardBeyond.Client;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Systems;
using Xunit;

namespace WaywardBeyond.Tests;

public class AudioChannelSystemTests
{
    [Fact]
    public void FocusedAndUnpausedUsesConfiguredChannelVolumes()
    {
        var settings = new VolumeSettings();
        settings.Master.Set(0.8f);
        settings.Effects.Set(0.5f);
        settings.Interface.Set(0.25f);
        settings.Music.Set(0.75f);

        var window = new FakeWindowContext();
        var store = new DataStore();
        using var system = new AudioChannelSystem(settings, window, () => false);

        system.Tick(0f, store);

        Assert.Equal(0.8f, GetChannelVolume(store, system, "master"), precision: 4);
        Assert.Equal(0.4f, GetChannelVolume(store, system, "effects"), precision: 4);
        Assert.Equal(0.2f, GetChannelVolume(store, system, "interface"), precision: 4);
        Assert.Equal(0.6f, GetChannelVolume(store, system, "music"), precision: 4);
    }

    [Fact]
    public void UnfocusedAttenuatesAllChannelsByBackgroundSettingAndRestoresOnFocus()
    {
        var settings = new VolumeSettings();
        settings.Master.Set(0.8f);
        settings.Effects.Set(0.5f);
        settings.Interface.Set(0.5f);
        settings.Music.Set(1f);

        var window = new FakeWindowContext();
        var store = new DataStore();
        using var system = new AudioChannelSystem(settings, window, () => false);

        system.Tick(0f, store);
        Assert.Equal(0.8f, GetChannelVolume(store, system, "master"), precision: 4);

        //  Default background volume is 0% (muted).
        window.Unfocused?.Invoke();
        system.Tick(0f, store);

        Assert.Equal(0f, GetChannelVolume(store, system, "master"), precision: 4);
        Assert.Equal(0f, GetChannelVolume(store, system, "effects"), precision: 4);
        Assert.Equal(0f, GetChannelVolume(store, system, "interface"), precision: 4);
        Assert.Equal(0f, GetChannelVolume(store, system, "music"), precision: 4);

        //  Updating the background setting applies while still unfocused.
        settings.Background.Set(0.25f);
        system.Tick(0f, store);

        Assert.Equal(0.2f, GetChannelVolume(store, system, "master"), precision: 4);
        Assert.Equal(0.1f, GetChannelVolume(store, system, "effects"), precision: 4);
        Assert.Equal(0.1f, GetChannelVolume(store, system, "interface"), precision: 4);
        Assert.Equal(0.2f, GetChannelVolume(store, system, "music"), precision: 4);

        //  Regaining focus restores full configured volumes.
        window.Focused?.Invoke();
        system.Tick(0f, store);

        Assert.Equal(0.8f, GetChannelVolume(store, system, "master"), precision: 4);
        Assert.Equal(0.4f, GetChannelVolume(store, system, "effects"), precision: 4);
        Assert.Equal(0.4f, GetChannelVolume(store, system, "interface"), precision: 4);
        Assert.Equal(0.8f, GetChannelVolume(store, system, "music"), precision: 4);
    }

    [Fact]
    public void PausedAttenuatesAllChannelsByPausedSettingAndRestoresOnUnpause()
    {
        var settings = new VolumeSettings();
        settings.Master.Set(0.8f);
        settings.Effects.Set(0.5f);
        settings.Interface.Set(0.5f);
        settings.Music.Set(1f);

        var paused = false;
        var window = new FakeWindowContext();
        var store = new DataStore();
        using var system = new AudioChannelSystem(settings, window, () => paused);

        system.Tick(0f, store);
        Assert.Equal(0.8f, GetChannelVolume(store, system, "master"), precision: 4);

        //  Default paused volume is 50%.
        paused = true;
        system.Tick(0f, store);

        Assert.Equal(0.4f, GetChannelVolume(store, system, "master"), precision: 4);
        Assert.Equal(0.2f, GetChannelVolume(store, system, "effects"), precision: 4);
        Assert.Equal(0.2f, GetChannelVolume(store, system, "interface"), precision: 4);
        Assert.Equal(0.4f, GetChannelVolume(store, system, "music"), precision: 4);

        //  Custom paused setting updates immediately.
        settings.Paused.Set(0.25f);
        system.Tick(0f, store);

        Assert.Equal(0.2f, GetChannelVolume(store, system, "master"), precision: 4);
        Assert.Equal(0.1f, GetChannelVolume(store, system, "effects"), precision: 4);
        Assert.Equal(0.1f, GetChannelVolume(store, system, "interface"), precision: 4);
        Assert.Equal(0.2f, GetChannelVolume(store, system, "music"), precision: 4);

        //  Unpausing restores full configured volumes.
        paused = false;
        system.Tick(0f, store);

        Assert.Equal(0.8f, GetChannelVolume(store, system, "master"), precision: 4);
        Assert.Equal(0.4f, GetChannelVolume(store, system, "effects"), precision: 4);
        Assert.Equal(0.4f, GetChannelVolume(store, system, "interface"), precision: 4);
        Assert.Equal(0.8f, GetChannelVolume(store, system, "music"), precision: 4);
    }

    [Fact]
    public void PausedAndUnfocusedAppliesStrongerAttenuation()
    {
        var settings = new VolumeSettings();
        settings.Master.Set(1f);
        settings.Music.Set(1f);
        settings.Paused.Set(0.5f);
        settings.Background.Set(0.2f);

        var paused = true;
        var window = new FakeWindowContext();
        var store = new DataStore();
        using var system = new AudioChannelSystem(settings, window, () => paused);

        window.Unfocused?.Invoke();
        system.Tick(0f, store);

        Assert.Equal(0.2f, GetChannelVolume(store, system, "master"), precision: 4);
        Assert.Equal(0.2f, GetChannelVolume(store, system, "music"), precision: 4);

        //  When background volume is higher than paused volume, paused remains the floor.
        settings.Background.Set(0.8f);
        system.Tick(0f, store);

        Assert.Equal(0.5f, GetChannelVolume(store, system, "master"), precision: 4);
        Assert.Equal(0.5f, GetChannelVolume(store, system, "music"), precision: 4);
    }

    [Fact]
    public void OutOfRangeAttenuationSettingsAreClamped()
    {
        var settings = new VolumeSettings();
        settings.Master.Set(0.5f);
        settings.Paused.Set(2.5f);
        settings.Background.Set(-0.5f);

        var paused = true;
        var window = new FakeWindowContext();
        var store = new DataStore();
        using var system = new AudioChannelSystem(settings, window, () => paused);

        system.Tick(0f, store);
        Assert.Equal(0.5f, GetChannelVolume(store, system, "master"), precision: 4);

        window.Unfocused?.Invoke();
        system.Tick(0f, store);
        Assert.Equal(0f, GetChannelVolume(store, system, "master"), precision: 4);
    }

    [Fact]
    public void DisposeUnsubscribesFromWindowFocusEvents()
    {
        var settings = new VolumeSettings();
        settings.Master.Set(0.5f);

        var window = new FakeWindowContext();
        var store = new DataStore();
        var system = new AudioChannelSystem(settings, window, () => false);

        system.Tick(0f, store);
        system.Dispose();

        window.Unfocused?.Invoke();
        system.Tick(0f, store);

        Assert.Equal(0.5f, GetChannelVolume(store, system, "master"), precision: 4);
    }

    [Fact]
    public void DefaultConstructorTracksGlobalGamePauseState()
    {
        var settings = new VolumeSettings();
        settings.Master.Set(0.8f);

        var window = new FakeWindowContext();
        var store = new DataStore();
        using var system = new AudioChannelSystem(settings, window);

        GameState previous = WaywardBeyond.Client.WaywardBeyond.GameState.Get();
        try
        {
            WaywardBeyond.Client.WaywardBeyond.GameState.Set(GameState.Playing);
            system.Tick(0f, store);
            Assert.Equal(0.8f, GetChannelVolume(store, system, "master"), precision: 4);

            WaywardBeyond.Client.WaywardBeyond.GameState.Set(GameState.Paused);
            system.Tick(0f, store);
            Assert.Equal(0.4f, GetChannelVolume(store, system, "master"), precision: 4);

            WaywardBeyond.Client.WaywardBeyond.GameState.Set(GameState.MainMenu);
            system.Tick(0f, store);
            Assert.Equal(0.8f, GetChannelVolume(store, system, "master"), precision: 4);
        }
        finally
        {
            WaywardBeyond.Client.WaywardBeyond.GameState.Set(previous);
        }
    }

    private static float GetChannelVolume(DataStore store, AudioChannelSystem system, string name)
    {
        Assert.True(system.TryGetChannelEntity(name, out Uuid uuid));
        Assert.True(store.TryGet(uuid, out AudioChannel channel));
        return channel.Volume;
    }
}
