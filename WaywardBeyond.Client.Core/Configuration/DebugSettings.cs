using System.Diagnostics.CodeAnalysis;
using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Client.Core.Configuration;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Local")]
public sealed class DebugSettings : Config<DebugSettings>
{
    public DataBinding<bool> OverlayVisible { get; private set; } = new();
}