using System.Numerics;
using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>Gameplay behavior configuration.</summary>
public sealed class GameplayConfig : Config<GameplayConfig>
{
    /// <summary>Whether to autosave.</summary>
    public DataBinding<bool> Autosave { get; private set; } = new(true);

    /// <summary>The interval to perform an autosave at in milliseconds.</summary>
    public DataBinding<int> AutosaveIntervalMs { get; private set; } = new(1000 * 60 * 5);  //  Default to 5 minutes

    /// <summary>Whether control hints are enabled.</summary>
    public DataBinding<bool> ControlHints { get; private set; } = new(true);

    /// <summary>Whether the crosshair is enabled.</summary>
    public DataBinding<bool> Crosshair { get; private set; } = new(true);

    /// <summary>The position a character spawns at.</summary>
    public DataBinding<Vector3> DefaultSpawnPosition { get; private set; } = new(new Vector3(0f, 1f, 5f));
}
