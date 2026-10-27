using System.Linq;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Data;
using Xunit;

namespace Swordfish.Tests;

public class GameSaveOrderingTests
{
    [Fact]
    public void SavesAreOrderedNewestFirst()
    {
        GameSave[] saves =
        [
            CreateSave("older", 100),
            CreateSave("newest", 300),
            CreateSave("middle", 200),
        ];

        GameSaveService.SortByLastPlayed(saves);

        Assert.Equal(["newest", "middle", "older"], saves.Select(save => save.Name));
    }

    [Fact]
    public void EqualLastPlayedTimesKeepTheListingOrder()
    {
        GameSave[] saves =
        [
            CreateSave("first", 200),
            CreateSave("second", 200),
            CreateSave("third", 100),
        ];

        GameSaveService.SortByLastPlayed(saves);

        Assert.Equal(["first", "second", "third"], saves.Select(save => save.Name));
    }

    private static GameSave CreateSave(string name, long lastPlayedMs)
    {
        return new GameSave(
            name,
            new Level(
                default,
                0,
                _Playtime: new Playtime { LastPlayedMs = lastPlayedMs },
                _SpawnX: 0,
                _SpawnY: 0,
                _SpawnZ: 0,
                GameMode.Creative,
                name,
                name
            )
        );
    }
}
