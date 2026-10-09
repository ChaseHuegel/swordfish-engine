using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>
///     Gameplay tunables shared by the client and the server. The client reads the character autosave
///     cadence; the server reads the level autosave cadence.
/// </summary>
public sealed class GameplaySettings : Config<GameplaySettings>
{
    public DataBinding<bool> Autosave { get; private set; } = new(true);

    public DataBinding<int> AutosaveIntervalMs { get; private set; } = new(1000 * 60 * 5);  //  Default to 5 minutes

    public DataBinding<bool> ControlHints { get; private set; } = new(true);

    public DataBinding<bool> Crosshair { get; private set; } = new(true);
}
