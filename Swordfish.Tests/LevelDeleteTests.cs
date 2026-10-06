using System;
using System.IO;
using System.Reflection;
using DryIoc;
using Microsoft.Extensions.Logging.Abstractions;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Menu-time level delete routes through the server facade, defers to the host, and removes the level
/// files on the server thread.
/// </summary>
public class LevelDeleteTests
{
    private static readonly MethodInfo _update = typeof(ServerWorldHost)
        .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Fact]
    public void MenuDeleteRemovesLevelFilesAndAnswers()
    {
        string root = Path.Combine(Path.GetTempPath(), "wb_delete_" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new StorageSettings();
            settings.DataRoot.Set(root);
            var paths = new StoragePaths(settings);
            var catalog = new SqliteLevelCatalog(NullLogger<SqliteLevelCatalog>.Instance, paths, TestBricks.Map);

            Assert.True(catalog.Create("Doomed", seed: "5", GameMode.Creative, out string levelGuid));
            Assert.True(catalog.Exists(levelGuid));

            var pendingJoins = new PendingJoins();
            var pendingDeletes = new PendingLevelDeletes();
            var manager = new ServerLevelManager(pendingJoins, pendingDeletes, catalog, NullLoggerFactory.Instance);
            var host = new ServerWorldHost(new Container(), manager, pendingJoins, pendingDeletes, catalog, new NetworkingSettings(), NullLoggerFactory.Instance);

            var connection = new LocalConnection(new INetworkSerializer[]
            {
                new NsdMessageSerializer<DeleteLevelRequest>(),
                new NsdMessageSerializer<DeleteLevelResponse>(),
            });
            pendingJoins.Add(connection.Server);

            connection.Client.Send(new DeleteLevelRequest { LevelGuid = levelGuid });
            manager.Tick();
            _update.Invoke(host, [0f]);

            Assert.True(connection.Client.Receive<DeleteLevelResponse>().Success);
            Assert.False(catalog.Exists(levelGuid));
            Assert.False(Directory.Exists(paths.LevelDirectory(levelGuid)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}