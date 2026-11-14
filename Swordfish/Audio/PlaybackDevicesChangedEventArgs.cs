namespace Swordfish.Audio;

/// <summary>Describes the playback device state after a change.</summary>
public readonly struct PlaybackDevicesChangedEventArgs(
    IReadOnlyList<string> playbackDeviceNames,
    string? defaultPlaybackDeviceName,
    string? preferredPlaybackDeviceName,
    string? activePlaybackDeviceName,
    bool isPreferredPlaybackDeviceMissing
)
{
    public IReadOnlyList<string> PlaybackDeviceNames { get; } = playbackDeviceNames;

    public string? DefaultPlaybackDeviceName { get; } = defaultPlaybackDeviceName;

    public string? PreferredPlaybackDeviceName { get; } = preferredPlaybackDeviceName;

    public string? ActivePlaybackDeviceName { get; } = activePlaybackDeviceName;

    public bool IsPreferredPlaybackDeviceMissing { get; } = isPreferredPlaybackDeviceMissing;
}
