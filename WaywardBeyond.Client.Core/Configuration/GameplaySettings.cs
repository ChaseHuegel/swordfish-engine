using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Client.Core.Configuration;

public sealed class GameplaySettings : Config<GameplaySettings>
{
    public DataBinding<bool> Autosave { get; private set; } = new(true);
}