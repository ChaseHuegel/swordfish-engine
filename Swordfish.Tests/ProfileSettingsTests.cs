#nullable enable
using System.Collections.Generic;
using Swordfish.Library.Configuration;
using Swordfish.Library.Serialization.Toml.Mappers;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Data;
using Xunit;

namespace Swordfish.Tests;

public class ProfileSettingsTests
{
    static ProfileSettingsTests()
    {
        //  The app registers a binding mapper per literal type it binds. Tomlet mappers are
        //  process-global, so the tests register the same set the client does.
        new DataBindingTomlMapper<string>().Register();
        new DataBindingTomlMapper<int>().Register();
        new DataBindingTomlMapper<long>().Register();
        new DataBindingTomlMapper<SessionServer>().Register();
        new DataBindingTomlMapper<SessionSave>().Register();
        new DataBindingTomlMapper<List<SavedServer>>().Register();
    }

    [Fact]
    public void DefaultProfileHasNoRecordedSession()
    {
        var profile = new ProfileSettings();

        Assert.False(profile.LastSession.Save.Get().IsRecorded);
        Assert.Equal(ServerMode.Local, profile.LastSession.Server.Get().Mode);
        Assert.Empty(profile.LastSession.CharacterUuid.Get());
    }

    [Fact]
    public void RoundTripPreservesTheLastSession()
    {
        var profile = new ProfileSettings();
        profile.LastSession.Server.Set(new SessionServer
        {
            Mode = ServerMode.Remote,
            Host = "10.0.0.5",
            Port = 25565,
        });
        profile.LastSession.CharacterUuid.Set("1e1b0b5a-0000-4000-8000-000000000001");
        profile.LastSession.Save.Set(new SessionSave
        {
            Guid = "level-guid",
            Name = "Alpha",
            Seed = 42,
            GameMode = GameMode.Adventure,
            LastPlayedMs = 1730000000000,
            AgeMs = 900000,
        });
        profile.SavedServers.Set([new SavedServer { Name = "LAN", Host = "10.0.0.5", Port = 25565 }]);

        ProfileSettings loaded = Toml<ProfileSettings>.FromString(profile.ToString());

        SessionServer server = loaded.LastSession.Server.Get();
        Assert.Equal(ServerMode.Remote, server.Mode);
        Assert.Equal("10.0.0.5", server.Host);
        Assert.Equal(25565, server.Port);
        Assert.Equal("1e1b0b5a-0000-4000-8000-000000000001", loaded.LastSession.CharacterUuid.Get());

        SessionSave save = loaded.LastSession.Save.Get();
        Assert.True(save.IsRecorded);
        Assert.Equal("level-guid", save.Guid);
        Assert.Equal("Alpha", save.Name);
        Assert.Equal(42, save.Seed);
        Assert.Equal(GameMode.Adventure, save.GameMode);
        Assert.Equal(1730000000000, save.LastPlayedMs);
        Assert.Equal(900000, save.AgeMs);

        SavedServer saved = Assert.Single(loaded.SavedServers.Get());
        Assert.Equal("LAN", saved.Name);
        Assert.Equal("10.0.0.5", saved.Host);
        Assert.Equal(25565, saved.Port);
    }

    [Fact]
    public void ProfileWritesValuesWithoutBindingInternals()
    {
        var profile = new ProfileSettings();
        profile.LastSession.Server.Set(new SessionServer { Mode = ServerMode.Remote, Host = "10.0.0.5", Port = 25565 });
        profile.LastSession.Save.Set(new SessionSave { Guid = "level-guid", Name = "Alpha" });

        string text = profile.ToString();

        Assert.DoesNotContain("_data", text);
        Assert.DoesNotContain("IsRecorded", text);
        Assert.Contains("Mode = \"Remote\"", text);
        Assert.Contains("Guid = \"level-guid\"", text);
    }
}
