using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFormat;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Globalization;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Transport;
using NUnit.Framework;

namespace WaywardBeyond.Client.Tests;

/// <summary>
/// The client owns its character autosave. Only the local host asks the server to flush the level;
/// remote clients save their character and never send a level-save request.
/// </summary>
public class GameSaveManagerSaveTests
{
    private static readonly INetworkSerializer[] Serializers =
    [
        new NsdMessageSerializer<ListLevelsRequest>(),
        new NsdMessageSerializer<SaveLevelRequest>(),
        new NsdMessageSerializer<SaveLevelResponse>(),
    ];

    [Test]
    public void HostSaveRequestsAServerSave()
    {
        var connection = new LocalConnection(Serializers);
        var transport = new TransportManager(Serializers, NullLoggerFactory.Instance, new NetworkingSettings());
        transport.UseLocal(connection.Client);
        var storage = new RecordingCharacterStorage();
        GameSaveManager manager = CreateManager(transport, storage, new FakeWindowContext());

        RunPlaying(manager, () =>
        {
            manager.Save();

            Assert.That(storage.Saves, Is.EqualTo(1));
            Assert.That(connection.Server.Receive<SaveLevelRequest>().Success, Is.True, "The host must ask the server to save the level.");
        });
    }

    [Test]
    public void AutosaveSavesTheCharacterWithoutRequestingAServerSave()
    {
        var connection = new LocalConnection(Serializers);
        var transport = new TransportManager(Serializers, NullLoggerFactory.Instance, new NetworkingSettings());
        transport.UseLocal(connection.Client);
        var storage = new RecordingCharacterStorage();
        GameSaveManager manager = CreateManager(transport, storage, new FakeWindowContext());

        RunPlaying(manager, () =>
        {
            MethodInfo autosave = typeof(GameSaveManager).GetMethod("OnAutosave", BindingFlags.Instance | BindingFlags.NonPublic)!;
            autosave.Invoke(manager, [null]);

            Assert.That(storage.Saves, Is.EqualTo(1));
            Assert.That(connection.Server.Receive<SaveLevelRequest>().Success, Is.False, "Client autosave must not ask the server to save the level.");
        });
    }

    [Test]
    public void RemoteClientDoesNotRequestAServerSave()
    {
        var connection = new LocalConnection(Serializers);
        var transport = new TransportManager(Serializers, NullLoggerFactory.Instance, new NetworkingSettings());
        transport.UseLocal(new RemoteConnection(connection.Client));
        var storage = new RecordingCharacterStorage();
        GameSaveManager manager = CreateManager(transport, storage, new FakeWindowContext());

        RunPlaying(manager, () =>
        {
            manager.Save();

            Assert.That(storage.Saves, Is.EqualTo(1));
            Assert.That(connection.Server.Receive<SaveLevelRequest>().Success, Is.False, "Remote clients must not ask the server to save the level.");
        });
    }

    private static void RunPlaying(GameSaveManager manager, Action action)
    {
        GameState prior = WaywardBeyond.GameState.Get();
        WaywardBeyond.GameState.Set(GameState.Playing);

        try
        {
            action();
        }
        finally
        {
            WaywardBeyond.GameState.Set(prior);
            manager.Dispose();
        }
    }

    private static GameSaveManager CreateManager(TransportManager transport, RecordingCharacterStorage storage, FakeWindowContext window)
    {
        var localization = new FakeLocalization(new Dictionary<string, string>());
        var notifications = new NotificationService(NullLogger<NotificationService>.Instance, window);
        var formatter = new LocalizedFormatter(localization, new SmartFormatter());
        var saves = new CharacterSaveManager(NullLogger<CharacterSaveManager>.Instance, storage, new ActiveCharacterSave())
        {
            ActiveSave = new Character { Id = 1, Name = "Tester" },
        };
        var levels = new LevelsClient(transport);
        var gameSaveService = new GameSaveService(
            NullLogger<GameSaveService>.Instance,
            formatter,
            notifications,
            levels,
            new StubSaveMetaStorage()
        );

        return new GameSaveManager(
            NullLogger<GameSaveManager>.Instance,
            gameSaveService,
            window,
            new StubShortcutService(),
            saves,
            new GameplaySettings(),
            null!,
            null!,
            transport,
            new DataStore(),
            new ProfileSettings()
        ) {
            ActiveSave = new GameSave("Test Level", new Level { Guid = "level-1", Name = "Test Level" }),
        };
    }

    private sealed class RemoteConnection : IClientConnection
    {
        private readonly IClientConnection _inner;

        public RemoteConnection(in IClientConnection inner)
        {
            _inner = inner;
        }

        public bool IsConnected => _inner.IsConnected;

        public bool IsLocal => false;

        public Result Send<T>(in T message) => _inner.Send(message);

        public Result<T> Receive<T>() => _inner.Receive<T>();
    }

    private sealed class FakeLocalization(Dictionary<string, string> translations) : ILocalization
    {
        public string? GetString(string value) => translations.TryGetValue(value, out string? result) ? result : value;

        public string? GetString(string value, string cultureName) => GetString(value);

        public string? GetString(string value, System.Globalization.CultureInfo cultureInfo) => GetString(value);
    }

    private sealed class FakeWindowContext : IWindowContext
    {
        public Swordfish.Library.Types.DataBinding<double> UpdateDelta { get; } = new();
        public Swordfish.Library.Types.DataBinding<double> RenderDelta { get; } = new();
        public Vector2 Resolution => default;
        public Vector2 MonitorResolution => default;
        public Action? Loaded { get; set; }
        public Action? Closed { get; set; }
        public Action<double>? Render { get; set; }
        public Action<double>? Update { get; set; }
        public Action? Focused { get; set; }
        public Action? Unfocused { get; set; }
        public Action<Vector2>? Resized { get; set; }
        public Vector2 GetSize() => default;
        public void Close() { }
        public void SetIcon(Texture icon) { }
    }

    private sealed class StubShortcutService : IShortcutService
    {
        public bool RegisterShortcut(Shortcut shortcut) => true;
    }

    private sealed class RecordingCharacterStorage : ICharacterStorage
    {
        public int Saves { get; private set; }

        public Result<Character> GetCharacter(ulong id) => Result<Character>.FromFailure("stub");

        public IEnumerable<Character> GetAllCharacters() => [];

        public Result SaveCharacter(Character character)
        {
            Saves++;
            return Result.FromSuccess();
        }

        public Result DeleteCharacter(ulong id) => Result.FromSuccess();
    }

    private sealed class StubSaveMetaStorage : ISaveMetaStorage
    {
        public Result<SaveMeta> Get(string levelGuid) => Result<SaveMeta>.FromFailure("stub");

        public IEnumerable<KeyValuePair<string, SaveMeta>> GetAll() => [];

        public Result Save(string levelGuid, SaveMeta meta) => Result.FromSuccess();

        public Result Delete(string levelGuid) => Result.FromSuccess();
    }
}
