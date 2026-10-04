using System.Collections.Generic;
using WaywardBeyond.Client.Core.Configuration;
using NUnit.Framework;

namespace WaywardBeyond.Client.Core.Tests;

/// <summary>
/// ProfileSettings carries client-local state (last joined mode, saved servers) in profile.toml; a
/// save/load round trip must preserve the enum marker and the saved-server list.
/// </summary>
public class ProfileSettingsTests
{
    [Test]
    public void SavedServersRoundTripThroughToml()
    {
        var source = new ProfileSettings();
        source.LastServerMode.Set(LastServerMode.Remote);
        source.SavedServers.Set(new List<SavedServer>
        {
            new() { Name = "Home", Host = "192.168.1.10", Port = 7777 },
            new() { Name = "Alice's world", Host = "play.example.com", Port = 4242 },
        });

        string toml = source.ToString();
        ProfileSettings restored = ProfileSettings.FromString(toml);

        Assert.That(restored.LastServerMode.Get(), Is.EqualTo(LastServerMode.Remote));
        List<SavedServer> servers = restored.SavedServers.Get();
        Assert.That(servers, Has.Count.EqualTo(2));
        Assert.That(servers[0].Name, Is.EqualTo("Home"));
        Assert.That(servers[0].Host, Is.EqualTo("192.168.1.10"));
        Assert.That(servers[0].Port, Is.EqualTo(7777));
        Assert.That(servers[1].Name, Is.EqualTo("Alice's world"));
        Assert.That(servers[1].Port, Is.EqualTo(4242));
    }

    [Test]
    public void DefaultsToLocalModeWithNoServers()
    {
        ProfileSettings settings = ProfileSettings.FromString(ProfileSettings.FromString("").ToString());

        Assert.That(settings.LastServerMode.Get(), Is.EqualTo(LastServerMode.Local));
        Assert.That(settings.SavedServers.Get(), Is.Empty);
    }
}