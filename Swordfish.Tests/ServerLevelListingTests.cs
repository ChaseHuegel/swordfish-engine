using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Util;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Transport;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// A level created by one menu-time client must be visible to a later list request from another client.
/// The server catalog is the single source of truth; this pins that the create/list path shares it.
/// </summary>
public class ServerLevelListingTests
{
    private static readonly INetworkSerializer[] Serializers =
    [
        new NsdMessageSerializer<NewLevelRequest>(),
        new NsdMessageSerializer<NewLevelResponse>(),
        new NsdMessageSerializer<ListLevelsRequest>(),
        new NsdMessageSerializer<ListLevelsResponse>(),
    ];

    [Fact]
    public void ALevelCreatedByOneClientIsListedForAnother()
    {
        string root = Path.Combine(Path.GetTempPath(), "wb_list_" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new StorageSettings();
            settings.DataRoot.Set(root);
            var paths = new StoragePaths(settings);
            var catalog = new SqliteLevelCatalog(NullLogger<SqliteLevelCatalog>.Instance, paths, TestBricks.Map);

            var pendingJoins = new PendingJoins();
            var manager = new ServerLevelManager(pendingJoins, new PendingLevelDeletes(), catalog, TestPermissions.EmptyPolicy, new ConnectionClaims(), NullLoggerFactory.Instance);

            var creator = new LocalConnection(Serializers);
            var observer = new LocalConnection(Serializers);
            pendingJoins.Add(creator.Server);
            pendingJoins.Add(observer.Server);

            creator.Client.Send(new NewLevelRequest { Name = "Shared World", Seed = "seed", GameMode = 0 });
            manager.Tick();

            //  Creation is served on a worker thread; wait for its response before listing.
            Result<NewLevelResponse> created = default;
            bool createAnswered = WaitFor(() =>
            {
                created = creator.Client.Receive<NewLevelResponse>();
                return created.Success;
            });
            Assert.True(createAnswered && created.Value.Success, "The creator must receive a successful create response.");

            observer.Client.Send(new ListLevelsRequest { Dummy = 0 });
            manager.Tick();

            Level[]? levels = null;
            bool listed = WaitFor(() =>
            {
                Result<ListLevelsResponse> response = observer.Client.Receive<ListLevelsResponse>();
                if (!response.Success)
                {
                    return false;
                }

                levels = response.Value.Levels;
                return true;
            });

            Assert.True(listed, "The observer must receive a level listing.");
            Assert.NotNull(levels);
            Assert.Contains(levels!, level => level.Name == "Shared World");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static bool WaitFor(Func<bool> condition, int timeoutMs = 5000)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return false;
    }
}
