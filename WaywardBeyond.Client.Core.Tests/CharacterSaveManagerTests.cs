using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core;
using WaywardBeyond.Client.Core.Saves;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Tests;

/// <summary>
/// The character's own time played must measure session time only. The clock is stamped at session
/// start; a save immediately after joining must not fold the idle gap since the previous session into
/// the age.
/// </summary>
public class CharacterSaveManagerTests
{
    private const long IdleGapMs = 3_600_000;
    private const long PriorAgeMs = 2_400_000;

    private sealed class StubCharacterStorage(Character stored) : ICharacterStorage
    {
        public Character? LastSaved { get; private set; }

        public Result<Character> GetCharacter(ulong id) => Result<Character>.FromSuccess(stored);
        public IEnumerable<Character> GetAllCharacters() => [stored];
        public Result SaveCharacter(Character character)
        {
            LastSaved = character;
            return Result.FromSuccess();
        }
        public Result DeleteCharacter(ulong id) => Result.FromSuccess();
    }

    [Test]
    public void SaveRightAfterSessionBegin_CountsOnlySessionTime_NotIdleGap()
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var stored = new Character(
            WaywardBeyond.Version,
            Uuid.NewUuid().ToValue(),
            _LastPlayedMs: nowMs - IdleGapMs,
            _AgeMs: PriorAgeMs,
            _Name: "Test",
            _Strength: 5,
            _Precision: 5,
            _Awareness: 5,
            _Charisma: 5,
            _Education: 5,
            _Resolve: 5,
            _Body: 0,
            _ActiveInventorySlot: 0,
            _GameMode: GameMode.Creative,
            _Statistics: null,
            _Inventory: null
        );
        var storage = new StubCharacterStorage(stored);
        var saves = new CharacterSaveManager(NullLogger<CharacterSaveManager>.Instance, storage, new ActiveCharacterSave());

        WaywardBeyond.GameState.Set(GameState.Playing);

        Result<Character> loaded = saves.Load();
        Assert.That(loaded.Success, Is.True);

        //  Session start re-stamps the clock so the idle hour before it never counts as play.
        Assert.That(loaded.Value.LastPlayedMs, Is.GreaterThanOrEqualTo(nowMs - 1_000));

        //  A save an instant later must count the (near-zero) session time, never the idle hour.
        saves.Save(new DataStore());

        Assert.That(storage.LastSaved, Is.Not.Null);
        Assert.That(storage.LastSaved.Value.AgeMs, Is.GreaterThanOrEqualTo(PriorAgeMs));
        Assert.That(storage.LastSaved.Value.AgeMs, Is.LessThan(PriorAgeMs + 10_000));
    }
}