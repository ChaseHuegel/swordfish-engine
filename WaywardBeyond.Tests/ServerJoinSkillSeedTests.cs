using Krill;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay.Statistics;
using WaywardBeyond.Skills;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>
/// The join seed splits character save statistics into <see cref="SkillStateComponent"/> (known skills)
/// and <see cref="StatisticsComponent"/> (non-skill character statistics) identically on client and server.
/// </summary>
public class ServerJoinSkillSeedTests
{
    [Fact]
    public void SeedsKnownSkillStatisticsAndNonSkillStatisticsSeparately()
    {
        (SkillDatabase skills, _) = SkillDatabaseTests.CreateSharedFixture();
        var store = new DataStore();
        int player = store.Alloc();

        Statistic[] statistics =
        [
            new Statistic("mining", 25),
            new Statistic("bricks.broken:wb:rock", 12),
        ];

        CharacterStatisticsSeeder.SeedSkillState(store, player, statistics, skills);
        CharacterStatisticsSeeder.SeedStatistics(store, player, statistics, skills);

        Assert.True(store.TryGet(player, out SkillStateComponent state));
        Assert.Equal(25, state.GetXP("mining"));
        Assert.Equal(0, state.GetXP("bricks.broken:wb:rock"));

        Assert.True(store.TryGet(player, out StatisticsComponent stats));
        Assert.Equal(12f, stats.Get("bricks.broken:wb:rock"));
        Assert.False(stats.TryGet("mining", out _));
    }

    [Fact]
    public void SeedsEmptyComponentsWithoutStatistics()
    {
        (SkillDatabase skills, _) = SkillDatabaseTests.CreateSharedFixture();
        var store = new DataStore();
        int player = store.Alloc();

        CharacterStatisticsSeeder.SeedSkillState(store, player, statistics: null, skills);
        CharacterStatisticsSeeder.SeedStatistics(store, player, statistics: null, skills);

        Assert.True(store.TryGet(player, out SkillStateComponent state));
        Assert.Equal(0, state.GetXP("mining"));

        Assert.True(store.TryGet(player, out StatisticsComponent stats));
        Assert.False(stats.TryGet("mining", out _));
    }
}
