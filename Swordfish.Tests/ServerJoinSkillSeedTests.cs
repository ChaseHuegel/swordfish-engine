using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Server.Core.Systems;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using WaywardBeyond.Shared.Skills;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// The client owns the initial skill seed: the statistics carried on the join <see cref="CharacterSeed"/>
/// seed the server's transient, per-session skill state (filtered to known skills). The server persists
/// nothing. Gated headlessly with the same lightweight world service as the session-routing tests.
/// </summary>
public class ServerJoinSkillSeedTests
{
    private static ServerJoinSystem CreateJoinSystem(ServerConnectionHub hub, SessionManager sessions, DataStore store, SkillDatabase skills)
    {
        return new ServerJoinSystem(
            hub,
            sessions,
            new LevelSaveService(NullLogger<LevelSaveService>.Instance, new StubLevelCatalog(), TestBricks.Map),
            new NetworkReplicationSystem(hub, sessions, NullLogger<NetworkReplicationSystem>.Instance),
            TestInteractionSystem.Create(hub),
            NullLogger<ServerJoinSystem>.Instance,
            TestBricks.Map,
            skills
        );
    }

    [Fact]
    public void JoinSeedsSkillStateFromClientStatistics()
    {
        SkillDatabase skills = SkillDatabaseTests.CreateSharedSkillDatabase();

        var hub = new ServerConnectionHub();
        var connection = new LocalConnection(new INetworkSerializer[]
        {
            new NsdMessageSerializer<JoinRequest>(),
            new NsdMessageSerializer<JoinAccept>(),
            new NsdMessageSerializer<LevelStreamComplete>(),
        });
        Uuid clientId = hub.Add(connection.Server);

        var store = new DataStore();
        var sessions = new SessionManager();
        ServerJoinSystem join = CreateJoinSystem(hub, sessions, store, skills);

        connection.Client.Send(new JoinRequest
        {
            CharacterId = 7,
            PublicView = new PublicView { CharacterId = 7, Name = "Test", Body = "wb:m_human" },
            Seed = new CharacterSeed
            {
                CharacterId = 7,
                Name = "Test",
                Body = "wb:m_human",
                Statistics =
                [
                    new Statistic("mining", 25),
                    new Statistic("bricks.broken:wb:rock", 12),
                ],
            },
        });

        join.Tick(0f, store);

        Assert.True(connection.Client.Receive<JoinAccept>().Success);
        Assert.True(sessions.TryGetEntity(clientId, out int entity));
        Assert.True(store.TryGet(entity, out SkillStateComponent state));

        //  Known skills seed; non-skill statistics never enter the server's state.
        Assert.Equal(25, state.GetXP("mining"));
        Assert.Equal(0, state.GetXP("bricks.broken:wb:rock"));
    }

    [Fact]
    public void JoinWithNoStatisticsSeedsEmptySkillState()
    {
        SkillDatabase skills = SkillDatabaseTests.CreateSharedSkillDatabase();

        var hub = new ServerConnectionHub();
        var connection = new LocalConnection(new INetworkSerializer[]
        {
            new NsdMessageSerializer<JoinRequest>(),
            new NsdMessageSerializer<JoinAccept>(),
            new NsdMessageSerializer<LevelStreamComplete>(),
        });
        Uuid clientId = hub.Add(connection.Server);

        var store = new DataStore();
        var sessions = new SessionManager();
        ServerJoinSystem join = CreateJoinSystem(hub, sessions, store, skills);

        //  New characters join without a statistics seed.
        connection.Client.Send(new JoinRequest
        {
            CharacterId = 8,
            PublicView = new PublicView { CharacterId = 8, Name = "New", Body = "wb:m_human" },
        });

        join.Tick(0f, store);

        Assert.True(sessions.TryGetEntity(clientId, out int entity));
        Assert.True(store.TryGet(entity, out SkillStateComponent state));
        Assert.True(state.XPBySkillId.Count == 0);
    }
}