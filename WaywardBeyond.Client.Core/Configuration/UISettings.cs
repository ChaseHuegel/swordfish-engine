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
}