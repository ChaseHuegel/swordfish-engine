using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>Region editing and blueprint size limit configuration. A value of 0 disables that cap.</summary>
public sealed record RegionConfig
{
    /// <summary>Maximum edge length in voxels along any axis. 0 means unlimited.</summary>
    public DataBinding<int> MaxEdge { get; private set; } = new(64);

    /// <summary>Maximum total bounding volume in voxels for one region operation. 0 means unlimited.</summary>
    public DataBinding<int> MaxVolume { get; private set; } = new(65_536);

    /// <summary>Maximum serialized region payload or blueprint file size in bytes. 0 means unlimited.</summary>
    public DataBinding<int> MaxPayloadBytes { get; private set; } = new(4 * 1024 * 1024);
}
