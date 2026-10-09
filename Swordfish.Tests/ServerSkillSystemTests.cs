using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Sessions;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Skills;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Headless coverage for the authoritative skill progression engine: XP accumulation seeded from the
/// client's join statistics, per-grant <see cref="SkillStateUpdateMessage"/> delivery to the acting
/// player's client, and the session reverse-lookup it depends on. Runs over the real shared skill tomls.
/// </summary>
public class ServerSkillSystemTests
{
    private sealed class Fixture
    {
        public SkillDatabase Skills { get; } = SkillDatabaseTests.CreateSharedSkillDatabase();
        public ServerConnectionHub Hub { get; } = new();
        public SessionManager Sessions { get; } = new();
        public DataStore Store { get; } = new();
        public LocalConnection Connection { get; }
        public int Player { get; }
        public ServerSkillSystem System { get; }

        public Fixture()
        {
            Connection = new LocalConnection(new INetworkSerializer[]
            {
                new NsdMessageSerializer<SkillStateUpdateMessage>(),
            });
            Uuid clientId = Hub.Add(Connection.Server);

            Player = Store.Alloc(new SkillStateComponent([]), new NetworkComponent());
            Sessions.Register(Store, Player, clientId, new Session(1));

            System = new ServerSkillSystem(Skills, Sessions, Hub, NullLogger<ServerSkillSystem>.Instance);
        }

        public Result<SkillStateUpdateMessage> ReceiveUpdate()
        {
            return Connection.Client.Receive<SkillStateUpdateMessage>();
        }
    }

    [Fact]
    public void BreakingARockGrantsMiningXP()
    {
        Fixture fixture = new();
        ushort rock = TestBricks.Map.Id("wb:rock");

        fixture.System.OnInteractionApplied(fixture.Store, fixture.Player, rock, isBreak: true);

        Result<SkillStateUpdateMessage> update = fixture.ReceiveUpdate();
        Assert.True(update.Success);
        Assert.Equal("mining", update.Value.SkillId);
        Assert.Equal(1, update.Value.GainedXP);
        Assert.Equal(1, update.Value.TotalXP);

        //  One XP is in no level along the mining curve (level 0, xp-into-level 1).
        Assert.Equal(0, update.Value.Level);
        Assert.Equal(1, update.Value.XPIntoLevel);
    }

    [Fact]
    public void RepeatedGrantsAccumulateXPInOrder()
    {
        Fixture fixture = new();
        ushort rock = TestBricks.Map.Id("wb:rock");

        fixture.System.OnInteractionApplied(fixture.Store, fixture.Player, rock, isBreak: true);
        fixture.System.OnInteractionApplied(fixture.Store, fixture.Player, rock, isBreak: true);

        Result<SkillStateUpdateMessage> first = fixture.ReceiveUpdate();
        Assert.Equal(1, first.Value.TotalXP);

        Result<SkillStateUpdateMessage> second = fixture.ReceiveUpdate();
        Assert.Equal(2, second.Value.TotalXP);
        Assert.Equal(1, second.Value.GainedXP);
    }

    [Fact]
    public void PlacingAPanelGrantsBuildingXPButNotMining()
    {
        Fixture fixture = new();
        ushort panel = TestBricks.Map.Id("wb:panel");

        //  A place only sources the right skill; mining has no place source.
        fixture.System.OnInteractionApplied(fixture.Store, fixture.Player, panel, isBreak: false);

        Result<SkillStateUpdateMessage> update = fixture.ReceiveUpdate();
        Assert.True(update.Success);
        Assert.Equal("building", update.Value.SkillId);
        Assert.Equal(1, update.Value.TotalXP);
    }

    [Fact]
    public void AnUnrecognizedBrickGrantsNoXP()
    {
        Fixture fixture = new();

        fixture.System.OnInteractionApplied(fixture.Store, fixture.Player, 999, isBreak: true);

        Assert.False(fixture.ReceiveUpdate().Success);
    }

    [Fact]
    public void ExistingSeededXPAdvancesLevels()
    {
        Fixture fixture = new();
        fixture.Store.AddOrUpdate(fixture.Player, new SkillStateComponent(new Dictionary<string, long> { ["mining"] = 61 }));

        ushort rock = TestBricks.Map.Id("wb:rock");
        fixture.System.OnInteractionApplied(fixture.Store, fixture.Player, rock, isBreak: true);

        Result<SkillStateUpdateMessage> update = fixture.ReceiveUpdate();
        Assert.True(update.Success);
        Assert.Equal(62, update.Value.TotalXP);
        Assert.Equal(1, update.Value.Level);
        Assert.Equal(1, update.Value.XPIntoLevel);
    }
}