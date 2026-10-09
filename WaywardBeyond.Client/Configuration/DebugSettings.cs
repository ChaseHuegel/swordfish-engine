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
}