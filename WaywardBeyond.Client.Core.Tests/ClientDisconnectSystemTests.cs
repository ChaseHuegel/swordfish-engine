using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Client.Core.Saves;
using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Client.Core.UI;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Serialization;
using NUnit.Framework;

namespace WaywardBeyond.Client.Core.Tests;

/// <summary>
/// A lost remote server must drive the client back to the menu on the ECS thread: the character save runs
/// while still in <c>Playing</c>, the world teardown is requested, the state drops to the menu, a
/// connection-lost toast is pushed, and the dead transport is dropped so later sends fail instead of hang.
/// </summary>
public class ClientDisconnectSystemTests
{
    private sealed class FakeLocalization : ILocalization
    {
        private readonly Dictionary<string, string> _translations;

        public FakeLocalization(Dictionary<string, string> translations)
        {
            _translations = translations;
        }

        public string? GetString(string value) => _translations.TryGetValue(value, out string? s) ? s : value;
        public string? GetString(string value, string cultureName) => GetString(value);
        public string? GetString(string value, CultureInfo cultureInfo) => GetString(value);
    }

    private sealed class FakeWindowContext : IWindowContext
    {
        public Swordfish.Library.Types.DataBinding<double> UpdateDelta { get; } = new();
        public Swordfish.Library.Types.DataBinding<double> RenderDelta { get; } = new();
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
    public void RemoteShutdownSavesReturnsToMenuAndDropsTransport()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var transportManager = new TransportManager(
            new INetworkSerializer[] { new NsdMessageSerializer<LeaveGameRequest>() },
            NullLoggerFactory.Instance,
            new NetworkingSettings()
        );

        var localization = new FakeLocalization(new Dictionary<string, string>
        {
            ["notification.connection.lost"] = "Connection to the server was lost.",
        });
        var window = new FakeWindowContext();
        var notifications = new NotificationService(NullLogger<NotificationService>.Instance, window);
        var saves = new CharacterSaveManager(NullLogger<CharacterSaveManager>.Instance, new StubCharacterStorage(), new ActiveCharacterSave());
        saves.ActiveSave = new Character { Id = 1, Name = "Tester" };
        var cleanup = new ClientCleanupSystem(NullLogger<ClientCleanupSystem>.Instance);
        var system = new ClientDisconnectSystem(transportManager, saves, cleanup, notifications, localization);

        GameState prior = WaywardBeyond.GameState.Get();
        WaywardBeyond.GameState.Set(GameState.Playing);

        try
        {
            Result connect = transportManager.ConnectRemote("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
            Assert.That(connect.Success, Is.True);

            using TcpClient serverSide = listener.AcceptTcpClient();
            serverSide.Close(); //  The server goes away.

            //  Wait for the ECS tick to process the disconnect (the transport is dropped as it does so).
            var store = new DataStore();
            var deadline = Environment.TickCount + 5000;
            while (transportManager.Active != null && Environment.TickCount < deadline)
            {
                system.Tick(0f, store);
                Thread.Sleep(10);
            }

            window.Update?.Invoke(0.016);

            Assert.That(transportManager.Active, Is.Null, "The dead remote transport should be dropped.");
            Assert.That(WaywardBeyond.GameState.Get(), Is.EqualTo(GameState.MainMenu));

            List<NotificationState> toasts = [.. notifications.GetActiveNotifications(NotificationType.Toast)];
            Assert.That(toasts, Has.Count.EqualTo(1));
            Assert.That(toasts[0].Notification.Text, Is.EqualTo("Connection to the server was lost."));
        }
        finally
        {
            WaywardBeyond.GameState.Set(prior);
            transportManager.Disconnect();
            listener.Stop();
        }
    }
}