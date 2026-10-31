using System.Diagnostics.CodeAnalysis;
using Swordfish.Library.Configuration;
using Swordfish.Library.Types;
using Tomlet.Attributes;

namespace WaywardBeyond.Client.Configuration;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Local")]
public sealed class DebugSettings : Config<DebugSettings>
{
    [TomlNonSerialized]
    public DataBinding<bool> OverlayVisible { get; private set; } = new();

    /// <summary>Controls the voxel chunk debug boxes. Only visible while the debug overlay is open.</summary>
    public DataBinding<bool> VoxelGizmosVisible { get; private set; } = new();
}