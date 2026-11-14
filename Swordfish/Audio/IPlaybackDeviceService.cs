namespace Swordfish.Audio;

/// <summary>Reads and selects the playback device for channels that use the default device.</summary>
public interface IPlaybackDeviceService
{
    /// <summary>Gets the names of the initialized playback devices, in ordinal order.</summary>
    IReadOnlyList<string> PlaybackDeviceNames { get; }

    /// <summary>Gets the name of the system default device, if one is marked.</summary>
    string? DefaultPlaybackDeviceName { get; }

    /// <summary>Gets the preferred device. Null means follow the system default.</summary>
    string? PreferredPlaybackDeviceName { get; }

    /// <summary>Gets the device that default channels use now.</summary>
    string? ActivePlaybackDeviceName { get; }

    /// <summary>Gets whether the preference names a device that is not available.</summary>
    bool IsPreferredPlaybackDeviceMissing { get; }

    /// <summary>Raised after a scan or a preference change alters the device state.</summary>
    event EventHandler<PlaybackDevicesChangedEventArgs>? PlaybackDevicesChanged;

    /// <summary>Sets or clears the preferred device. Null or empty clears it.</summary>
    void SetPreferredPlaybackDevice(string? name);
}
