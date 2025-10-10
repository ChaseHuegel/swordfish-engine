using Swordfish.Library.Configuration;
using Tomlet.Attributes;

namespace WaywardBeyond.Client.Core.Configuration;

public sealed class Settings : Config<Settings>
{
    [TomlDoNotInlineObject]
    public ControlSettings Control { get; private set; } = new();
    
    [TomlDoNotInlineObject]
    public DisplaySettings Display { get; private set; } = new();
}