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

    /// <summary>Whether reactive first-person view-model sway is enabled.</summary>
    public DataBinding<bool> FirstPersonSway { get; private set; } = new(true);

    /// <summary>The closing impact speed in meters per second below which a collision deals no damage.</summary>
    public DataBinding<float> CollisionSafeSpeed { get; private set; } = new(6f);

    /// <summary>The kinetic damage dealt per meter per second of impact speed above <see cref="CollisionSafeSpeed"/>.</summary>
    public DataBinding<float> CollisionDamagePerSpeed { get; private set; } = new(5f);

    /// <summary>The most kinetic damage one collision can deal before resistance mitigation.</summary>
    public DataBinding<float> CollisionMaxDamage { get; private set; } = new(100f);

    /// <summary>The window in seconds during which a character ignores further collision damage.</summary>
    public DataBinding<float> CollisionCooldownSeconds { get; private set; } = new(0.25f);

    /// <summary>The position a character spawns at.</summary>
    public DataBinding<Vector3> DefaultSpawnPosition { get; private set; } = new(new Vector3(0f, 1f, 5f));

    /// <inheritdoc cref="RegionConfig"/>
    public RegionConfig Regions { get; private set; } = new();

    /// <inheritdoc cref="SurvivalConfig"/>
    public SurvivalConfig Survival { get; private set; } = new();
}
