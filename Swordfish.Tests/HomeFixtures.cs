using System;
using System.Collections.Generic;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Bodies;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Skills;

namespace Swordfish.Tests;

internal static class HomeFixtures
{
    public const string SAVE_NAME = "Frontier Base";
    public const string CHARACTER_UUID = "1";

    public static HomePage CreateHome(bool withSession)
    {
        var localization = new TestLocalization();
        var profile = new ProfileSettings();
        if (withSession)
        {
            profile.LastSession.Save.Set(new SessionSave
            {
                Guid = "save-guid",
                Name = SAVE_NAME,
                Seed = 42,
                GameMode = GameMode.Creative,
                LastPlayedMs = 1_700_000_000_000,
                AgeMs = 3_600_000,
            });
            profile.LastSession.CharacterUuid.Set(CHARACTER_UUID);
        }

        var characters = new StubCharacterStorage(withSession);
        var assets = new CharacterAssetService(
            new StubBodyDatabase(),
            new StubTextureDatabase(),
            new StubFileParseService()
        );
        var seeds = new SeedPhraseService(new StubTagDatabase());
        var modalMenu = new ModalMenu(
            null!,
            null!,
            new StubShortcutService(),
            null!,
            [new StubModalPage(PlaytestNoticeModal.Modal)]
        );

        return new HomePage(
            null!,
            null!,
            localization,
            new GameSaveService(null!, null!, null!, null!, null!),
            null!,
            characters,
            assets,
            new StubSkillDatabase(),
            null!,
            profile,
            new ContinueService(null!, null!, profile, null!, null!, null!, characters, localization, null!),
            null!,
            modalMenu,
            seeds
        );
    }

    private sealed class StubShortcutService : IShortcutService
    {
        public bool RegisterShortcut(Shortcut shortcut) => true;
    }

    private sealed class StubModalPage(Modal id) : IMenuPage<Modal>
    {
        public Modal ID => id;

        public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu) => Result.FromSuccess();
    }

    private sealed class StubCharacterStorage(bool hasCharacter) : ICharacterStorage
    {
        private readonly Character _character = new(
            default,
            1UL,
            _Playtime: new Playtime(),
            "Ada",
            0, 0, 0, 0, 0, 0,
            _Body: "human",
            _ActiveInventorySlot: 0,
            _GameMode: GameMode.Creative,
            _Statistics: null,
            _Inventory: null
        );

        public Result<Character> GetCharacter(ulong id) => throw new NotSupportedException();

        public IReadOnlyList<Character> GetAllCharacters() => hasCharacter ? [_character] : [];

        public Result SaveCharacter(Character character) => throw new NotSupportedException();

        public Result DeleteCharacter(ulong id) => throw new NotSupportedException();
    }

    private sealed class StubTagDatabase : IAssetDatabase<LocalizedTags>
    {
        private readonly LocalizedTags _tags = new(new Dictionary<string, List<string>>
        {
            [SeedPhraseService.ADJECTIVES_TAG] = Words("adjective"),
            [SeedPhraseService.SUBJECTS_TAG] = Words("subject"),
            [SeedPhraseService.VERBS_TAG] = Words("verb"),
            [SeedPhraseService.DESTINATIONS_TAG] = Words("destination"),
        });

        public Result<LocalizedTags> Get(string id) => Result<LocalizedTags>.FromSuccess(_tags);

        private static List<string> Words(string prefix)
        {
            var words = new List<string>(SeedPhraseService.DICTIONARY_SIZE);
            for (var i = 0; i < SeedPhraseService.DICTIONARY_SIZE; i++)
            {
                words.Add($"{prefix}{i}");
            }

            return words;
        }
    }
}
