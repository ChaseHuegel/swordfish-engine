using System.Diagnostics.CodeAnalysis;
using Swordfish.Library.Configuration;
using Swordfish.Library.Types;
using Tomlet.Attributes;

namespace WaywardBeyond.Client.Core.Configuration;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Local")]
public sealed class UISettings : Config<UISettings>
{
    [TomlNonSerialized]
    public DataBinding<bool> Visible { get; private set; } = new(true);

    /// <summary>Distance at which remote player name tags render, in world units; 0 disables tags.</summary>
    public DataBinding<int> NameplateDistance { get; private set; } = new(32);
}