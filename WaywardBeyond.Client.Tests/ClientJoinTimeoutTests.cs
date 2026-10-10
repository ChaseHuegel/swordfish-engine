using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Serialization;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using WaywardBeyond.Client;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;
using NUnit.Framework;

namespace WaywardBeyond.Client.Tests;

/// <summary>
/// A join whose world stream never completes must not stall Loading forever: after
/// <c>NetworkingSettings.JoinStreamTimeoutMs</c> the client aborts the join, returns to the menu with
/// the connection-lost notice, and drops the transport.
/// </summary>
public class ClientJoinTimeoutTests
{
    private sealed class StalledConnection : IClientConnection
    {
        public bool IsConnected => true;
        public bool IsLocal => false;

        public Result Send<T>(in T message) => Result.FromSuccess();

        public Result<T> Receive<T>()
        {
            return Result<T>.FromFailure("No server replies.");
        }
    }

    private sealed class NoConnection : IClientConnection
    {
        public bool IsConnected => false;
        public bool IsLocal => false;

        public Result Send<T>(in T message) => Result.FromFailure("No active connection.");

        public Result<T> Receive<T>()
        {
            return Result<T>.FromFailure("No active connection.");
        }
    }

    private sealed class StubBrickRegistry : IBrickRegistry
    {
        public ushort Id(string name) => 0;
        public string? Name(ushort id) => null;
        public int Count => 0;
    }

    private sealed class FakeLocalization : ILocalization
    {
        public string? GetString(string value) => value;
        public string? GetString(string value, string cultureName) => value;
        public string? GetString(string value, CultureInfo cultureInfo) => value;
    }

    private sealed class FakeWindowContext : IWindowContext
    {
        public DataBinding<double> UpdateDelta { get; } = new();
        public DataBinding<double> RenderDelta { get; } = new();
        public System.Numerics.Vector2 Resolution => default;
        public System.Numerics.Vector2 MonitorResolution => default;
        public System.Action? Loaded { get; set; }
        public System.Action? Closed { get; set; }
        public System.Action<double>? Render { get; set; }
        public System.Action<double>? Update { get; set; }
        public System.Action? Focused { get; set; }
        public System.Action? Unfocused { get; set; }
        public System.Action<System.Numerics.Vector2>? Resized { get; set; }
        public System.Numerics.Vector2 GetSize() => default;
        public void Close() { }
        public void SetIcon(Texture icon) { }
    }

    private sealed class StubCharacterStorage : ICharacterStorage
    {
        public Result<Character> GetCharacter(ulong id) => Result<Character>.FromFailure("stub");
        public IEnumerable<Character> GetAllCharacters() => [];
        public Result SaveCharacter(Character character) => Result.FromSuccess();
        public Result DeleteCharacter(ulong id) => Result.FromSuccess();
    }

    [Test]
    public void JoinThatNeverCompletesReturnsToMenuAfterTimeout()
    {
        var settings = new NetworkingConfig();
        settings.JoinStreamTimeoutMs.Set(100);

        var transportManager = new TransportManager(
            new INetworkSerializer[] { new NsdMessageSerializer<JoinRequest>() },
            NullLoggerFactory.Instance,
            settings,
            new TestUserClaimProvider()
        );

        var window = new FakeWindowContext();
        var notifications = new NotificationService(NullLogger<NotificationService>.Instance, window);
        var saves = new CharacterSaveManager(NullLogger<CharacterSaveManager>.Instance, new StubCharacterStorage(), new ActiveCharacterSave());
        var cleanup = new ClientCleanupSystem(NullLogger<ClientCleanupSystem>.Instance);
        var worlds = new LevelsClient(transportManager);
        var disconnectSystem = new ClientDisconnectSystem(transportManager, saves, cleanup, notifications, new FakeLocalization(), worlds);

        var joinSystem = new ClientJoinSystem(
            new StalledConnection(),
            new PlayerCharacterEntityBuilder(null!),
            new VoxelEntityBuilder(null!, new Shader("test"), new PBRTextureArrays(null!, null!, null!, null!, null!), null!, []),
            NullLogger<ClientJoinSystem>.Instance,
            new StubBrickRegistry(),
            disconnectSystem,
            settings
        );

        GameState prior = WaywardBeyond.GameState.Get();
        WaywardBeyond.GameState.Set(GameState.Loading);

        try
        {
            joinSystem.RequestJoin(new Character { Id = 1, Name = "Tester" }, "level-guid");
            var store = new DataStore();

            var deadline = Environment.TickCount + 5000;
            while (WaywardBeyond.GameState.Get() == GameState.Loading && Environment.TickCount < deadline)
            {
                joinSystem.Tick(0f, store);
                disconnectSystem.Tick(0f, store);
                System.Threading.Thread.Sleep(10);
            }

            Assert.That(WaywardBeyond.GameState.Get(), Is.EqualTo(GameState.MainMenu), "A timed-out join must return to the menu.");

            window.Update?.Invoke(0.016);

            List<NotificationState> toasts = [.. notifications.GetActiveNotifications(NotificationType.Toast)];
            Assert.That(toasts, Has.Count.EqualTo(1));
            Assert.That(toasts[0].Notification.Text, Is.EqualTo("notification.connection.lost"));
        }
        finally
        {
            WaywardBeyond.GameState.Set(prior);
            transportManager.Disconnect();
        }
    }

    [Test]
    public void JoinWithNoActiveConnectionFailsFastInsteadOfLoading()
    {
        var settings = new NetworkingConfig();

        var transportManager = new TransportManager(
            new INetworkSerializer[] { new NsdMessageSerializer<JoinRequest>() },
            NullLoggerFactory.Instance,
            settings,
            new TestUserClaimProvider()
        );

        var notifications = new NotificationService(NullLogger<NotificationService>.Instance, new FakeWindowContext());
        var saves = new CharacterSaveManager(NullLogger<CharacterSaveManager>.Instance, new StubCharacterStorage(), new ActiveCharacterSave());
        var cleanup = new ClientCleanupSystem(NullLogger<ClientCleanupSystem>.Instance);
        var disconnectSystem = new ClientDisconnectSystem(transportManager, saves, cleanup, notifications, new FakeLocalization(), new LevelsClient(transportManager));

        var joinSystem = new ClientJoinSystem(
            new NoConnection(),
            new PlayerCharacterEntityBuilder(null!),
            new VoxelEntityBuilder(null!, new Shader("test"), new PBRTextureArrays(null!, null!, null!, null!, null!), null!, []),
            NullLogger<ClientJoinSystem>.Instance,
            new StubBrickRegistry(),
            disconnectSystem,
            settings
        );

        GameState prior = WaywardBeyond.GameState.Get();
        WaywardBeyond.GameState.Set(GameState.Loading);

        try
        {
            joinSystem.RequestJoin(new Character { Id = 1, Name = "Tester" }, "level-guid");
            var store = new DataStore();

            //  Two ticks: the join send fails, the request is dropped, and the disconnect teardown runs.
            joinSystem.Tick(0f, store);
            disconnectSystem.Tick(0f, store);
            joinSystem.Tick(0f, store);

            Assert.That(WaywardBeyond.GameState.Get(), Is.EqualTo(GameState.MainMenu), "A failed join must leave Loading.");
        }
        finally
        {
            WaywardBeyond.GameState.Set(prior);
            transportManager.Disconnect();
        }
    }
}