using SoundFlow.Structs;

namespace Swordfish.Audio.SoundFlow;

/// <summary>Compares the current playback devices with a fresh enumeration.</summary>
internal static class PlaybackDeviceSet
{
    /// <summary>Adds names that are absent from the enumeration.</summary>
    public static void FindRemoved(IEnumerable<string> currentNames, DeviceInfo[] discovered, List<string> removed)
    {
        foreach (string name in currentNames)
        {
            if (!Contains(discovered, name))
            {
                removed.Add(name);
            }
        }
    }

    /// <summary>Finds the name of the default device, if one is marked.</summary>
    public static string? FindDefaultName(DeviceInfo[] discovered)
    {
        foreach (DeviceInfo deviceInfo in discovered)
        {
            if (deviceInfo.IsDefault)
            {
                return deviceInfo.Name;
            }
        }

        return null;
    }

    /// <summary>Returns whether the enumeration contains the device name.</summary>
    public static bool Contains(DeviceInfo[] discovered, string name)
    {
        foreach (DeviceInfo deviceInfo in discovered)
        {
            if (deviceInfo.Name == name)
            {
                return true;
            }
        }

        return false;
    }
}
