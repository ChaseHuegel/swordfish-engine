using Swordfish.ECS;
using WaywardBeyond.Data;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Skills;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// The join seed carries only statistics whose id names a known skill into the player's transient,
/// server-authoritative skill state.
/// </summary>
public class ServerJoinSkillSeedTests
{
    [Fact]
    public void SeedsKnownSkillStatistics()
    {
        (SkillDatabase skills, _) = SkillDatabaseTests.CreateSharedFixture();
        var store = new DataStore();
        int player = store.Alloc();

        ServerJoinSystem.SeedSkillState(store, player, new CharacterState
        {
            Statistics =
            [
                new Statistic("mining", 25),
                new Statistic("bricks.broken:wb:rock", 12),
            ],
        }, skills);

        Assert.True(store.TryGet(player, out SkillStateComponent state));
        Assert.Equal(25, state.GetXP("mining"));

        //  A statistic that does not name a skill never enters the server's state.
        Assert.Equal(0, state.GetXP("bricks.broken:wb:rock"));
    }

    [Fact]
    public void SeedsAnEmptyComponentWithoutStatistics()
    {
        (SkillDatabase skills, _) = SkillDatabaseTests.CreateSharedFixture();
        var store = new DataStore();
        int player = store.Alloc();

        ServerJoinSystem.SeedSkillState(store, player, new CharacterState(), skills);

        Assert.True(store.TryGet(player, out SkillStateComponent state));
        Assert.Equal(0, state.GetXP("mining"));
    }
}
