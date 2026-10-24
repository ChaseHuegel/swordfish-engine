using System;
using System.Collections.Generic;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Server.Generation;
using Xunit;

namespace Swordfish.Tests;

/// <summary>Pins the level generation tuning the server rejects at startup and at create time.</summary>
public class LevelGeneratorConfigTests
{
    [Fact]
    public void DefaultsAreValid()
    {
        new LevelGeneratorConfig().Validate();
    }

    [Fact]
    public void ZeroOctavesIsRejected()
    {
        var config = new LevelGeneratorConfig();
        config.Asteroid.NoiseOctaves.Set(0);

        Assert.Throws<InvalidOperationException>(config.Validate);
    }

    [Fact]
    public void ZeroRadiusDivisorIsRejected()
    {
        var config = new LevelGeneratorConfig();
        config.Asteroid.RadiusMinDivisor.Set(0);

        Assert.Throws<InvalidOperationException>(config.Validate);
    }

    [Fact]
    public void InvertedRadiusRangeIsRejected()
    {
        var config = new LevelGeneratorConfig();
        config.AsteroidMinRadius.Set(50);
        config.AsteroidMaxRadius.Set(10);

        Assert.Throws<InvalidOperationException>(config.Validate);
    }

    [Fact]
    public void EmptyMaterialsIsRejected()
    {
        var config = new LevelGeneratorConfig();
        config.Asteroid.Materials.Set([]);

        Assert.Throws<InvalidOperationException>(config.Validate);
    }

    [Fact]
    public void UnknownMaterialFailsAtGeneration()
    {
        var config = new LevelGeneratorConfig();
        config.Asteroid.Materials.Set(["wb:missing"]);
        IBrickRegistry registry = BrickRegistry.FromNames(["wb:rock", "wb:ice"]);

        Assert.Throws<InvalidOperationException>(() => new LevelGenerator(1, registry, config));
    }

    [Fact]
    public void ImpossiblePlacementIsBounded()
    {
        var config = new LevelGeneratorConfig();
        config.AsteroidCount.Set(32);
        config.WorldSpan.Set(1);
        config.WorldHeight.Set(1);
        config.AsteroidMinRadius.Set(40);
        config.AsteroidMaxRadius.Set(40);
        IBrickRegistry registry = BrickRegistry.FromNames(["wb:rock", "wb:ice"]);

        //  Every candidate intersects: the generator must give up instead of spinning the server thread.
        Assert.Throws<InvalidOperationException>(() => new LevelGenerator(1, registry, config).Generate());
    }
}
