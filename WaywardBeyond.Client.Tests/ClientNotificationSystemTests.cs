using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFormat;
using SmartFormat.Core.Settings;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Globalization;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Data;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Transport;

namespace WaywardBeyond.Client.Tests;

/// <summary>
/// The client is a dumb display for skills: remote notifications are localized against the client's own
/// locale files (servers send keys, never text) and pushed by type, and authoritative skill totals are
/// written into the client-owned save. No skill math happens here.
/// </summary>
public class ClientNotificationSystemTests
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

    private sealed class Harness
    {
        public FakeWindowContext Window { get; } = new();
        public NotificationService Notifications { get; }
        public CharacterSaveManager Saves { get; }
        public LocalConnection Connection { get; }
        public ClientNotificationSystem System { get; }

        public Harness(Dictionary<string, string> translations)
        {
            var localization = new FakeLocalization(translations);
            var formatter = new LocalizedFormatter(localization, Smart.CreateDefaultSmartFormat(new SmartSettings()));

            Notifications = new NotificationService(NullLogger<NotificationService>.Instance, Window);
            Saves = new CharacterSaveManager(NullLogger<CharacterSaveManager>.Instance, new StubCharacterStorage(), new ActiveCharacterSave());

            Connection = new LocalConnection(new INetworkSerializer[]
            {
                new NsdMessageSerializer<NotificationMessage>(),
                new NsdMessageSerializer<SkillStateUpdateMessage>(),
            });

            System = new ClientNotificationSystem(Connection.Client, Notifications, formatter, localization, Saves, NullLogger<ClientNotificationSystem>.Instance);
        }

        public List<NotificationState> Pump()
        {
            System.Tick(0f, new DataStore());
            Window.Update?.Invoke(0.016);
            List<NotificationState> states = [.. Notifications.GetActiveNotifications(NotificationType.Toast)];
            states.AddRange(Notifications.GetActiveNotifications(NotificationType.Bar));
            return states;
        }
    }

    [Test]
    public void RemoteXpBarIsLocalizedAndPushed()
    {
        var harness = new Harness(new Dictionary<string, string>
        {
            ["skill.name.mining"] = "Mining",
            ["notification.skill.bar"] = "{0} - {1}",
        });

        harness.Connection.Server.Send(new NotificationMessage
        {
            Type = (byte)NotificationType.Bar,
            Key = "notification.skill.bar",
            Args = ["skill.name.mining", "5"],
            ID = "mining",
            Amount = 0.5f,
        });

        List<NotificationState> states = harness.Pump();

        Assert.That(states, Has.Count.EqualTo(1));
        NotificationState state = states[0];
        Assert.That(state.Notification.Type, Is.EqualTo(NotificationType.Bar));
        Assert.That(state.Notification.Text, Is.EqualTo("Mining - 5"));
        Assert.That(state.Notification.ID, Is.EqualTo("mining"));
        Assert.That(state.Notification.Amount, Is.EqualTo(0.5f));
    }

    [Test]
    public void RemoteLevelUpToastIsLocalizedAndPushed()
    {
        var harness = new Harness(new Dictionary<string, string>
        {
            ["skill.name.building"] = "Building",
            ["notification.skill.levelUp"] = "{0} increased from {1} to {2}!",
        });

        harness.Connection.Server.Send(new NotificationMessage
        {
            Type = (byte)NotificationType.Toast,
            Key = "notification.skill.levelUp",
            Args = ["skill.name.building", "2", "3"],
        });

        List<NotificationState> states = harness.Pump();
        Assert.That(states, Has.Count.EqualTo(1));
        NotificationState state = states[0];
        Assert.That(state.Notification.Type, Is.EqualTo(NotificationType.Toast));
        Assert.That(state.Notification.Text, Is.EqualTo("Building increased from 2 to 3!"));
    }

    [Test]
    public void ArgsThatAreNotKeysPassThroughUntouched()
    {
        var harness = new Harness(new Dictionary<string, string>
        {
            ["notification.sample"] = "Value {0}",
        });

        harness.Connection.Server.Send(new NotificationMessage
        {
            Type = (byte)NotificationType.Toast,
            Key = "notification.sample",
            Args = ["42"],
        });

        List<NotificationState> states = harness.Pump();
        Assert.That(states, Has.Count.EqualTo(1));
        NotificationState state = states[0];
        Assert.That(state.Notification.Text, Is.EqualTo("Value 42"));
    }

    [Test]
    public void SkillStateUpdatePersistsAuthoritativeTotalToActiveSave()
    {
        var harness = new Harness(new Dictionary<string, string>());
        harness.Saves.ActiveSave = new Character
        {
            Id = 7,
            Name = "Test",
            Statistics = [new Statistic("building", 10)],
        };

        harness.Connection.Server.Send(new SkillStateUpdateMessage
        {
            SkillId = "mining",
            TotalXP = 42,
            Level = 1,
            XPIntoLevel = 42,
            GainedXP = 42,
        });

        harness.System.Tick(0f, new DataStore());

        Character? saved = harness.Saves.ActiveSave;
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved.Value.Statistics, Is.Not.Null);

        bool found = false;
        foreach (Statistic statistic in saved.Value.Statistics!)
        {
            if (statistic.ID == "mining")
            {
                Assert.That(statistic.Value, Is.EqualTo(42));
                found = true;
            }
        }

        Assert.That(found, Is.True, "The authoritative mining total should be written to the save.");
    }

    [Test]
    public void SkillStateUpdateWithNoActiveSaveIsIgnored()
    {
        var harness = new Harness(new Dictionary<string, string>());

        harness.Connection.Server.Send(new SkillStateUpdateMessage
        {
            SkillId = "mining",
            TotalXP = 42,
        });

        Assert.DoesNotThrow(() => harness.System.Tick(0f, new DataStore()));
    }
}