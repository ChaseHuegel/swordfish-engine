using System;
using System.IO;
using System.Collections.Generic;
using Reef;
using Reef.Text;
using Reef.UI;
using Swordfish.Library.Collections;
using WaywardBeyond.Bodies;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Skills;

namespace Swordfish.Tests;

/// <summary>A text engine that measures every glyph as eight pixels wide.</summary>
internal sealed class StubTextEngine : ITextEngine
{
    public ITypeface GetDefaultTypeface() => null!;

    public ITypeface[] GetTypefaces() => [];

    public bool TryGetTypeface(FontInfo fontInfo, out ITypeface? typeface)
    {
        typeface = null;
        return false;
    }

    public TextConstraints Measure(FontOptions fontOptions, string text) => MeasureText(fontOptions, text);

    public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length)
        => MeasureText(fontOptions, text.Substring(start, length));

    public TextLayout Layout(FontOptions fontOptions, string text) => LayoutText(fontOptions, text);

    public TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => LayoutText(fontOptions, text);

    public TextLayout Layout(FontOptions fontOptions, string text, int start, int length)
        => LayoutText(fontOptions, text.Substring(start, length));

    public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth)
        => LayoutText(fontOptions, text.Substring(start, length));

    public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => [text];

    public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth)
        => [text.Substring(start, length)];

    private static TextConstraints MeasureText(FontOptions fontOptions, string text)
    {
        int width = text.Length * 8;
        int height = Math.Max(1, fontOptions.Size);
        return new TextConstraints(width, height, width, height);
    }

    private static TextLayout LayoutText(FontOptions fontOptions, string text)
    {
        return new TextLayout(MeasureText(fontOptions, text), [], [], Math.Max(1, fontOptions.Size));
    }
}

/// <summary>A shortcut service that discards registrations.</summary>
internal sealed class StubShortcutService : IShortcutService
{
    public bool RegisterShortcut(Shortcut shortcut) => true;
}

/// <summary>A tag database with a full seed phrase dictionary.</summary>
internal sealed class StubTagDatabase : IAssetDatabase<LocalizedTags>
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

/// <summary>A character storage that holds at most one colonist.</summary>
internal sealed class StubCharacterStorage(bool hasCharacter) : ICharacterStorage
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

    public Result<Character> GetCharacter(ulong id) => Result<Character>.FromSuccess(_character);

    public IReadOnlyList<Character> GetAllCharacters() => hasCharacter ? [_character] : [];

    public Result SaveCharacter(Character character) => Result.FromSuccess();

    public Result DeleteCharacter(ulong id) => Result.FromSuccess();
}

internal sealed class StubBodyDatabase : IBodyDatabase
{
    private readonly Body _body = new(
        "human",
        "human",
        Gender.Neutral,
        new Dictionary<string, string[]> { ["standing"] = ["human_standing.png"] }
    );

    public string? DefaultId => "human";

    public IReadOnlyList<string> Species => ["human"];

    public IReadOnlyList<Gender> GetGenders(string species) => [Gender.Neutral];

    public Body? GetBody(string species, Gender gender) => _body;

    public Result<Body> Get(string id) => Result<Body>.FromSuccess(_body);
}

/// <summary>A texture database that returns a one-pixel texture.</summary>
internal sealed class StubTextureDatabase : IAssetDatabase<Texture>
{
    public Result<Texture> Get(string id) => Result<Texture>.FromSuccess(new Texture([], 1, 1, mipmaps: false));
}

/// <summary>A file parser that parses nothing.</summary>
internal sealed class StubFileParseService : IFileParseService
{
    public TResult Parse<TResult>(PathInfo path) => default!;

    public TResult Parse<TResult>(Stream stream) => throw new NotSupportedException();

    public bool TryParse<TResult>(PathInfo path, out TResult result)
    {
        result = default!;
        return false;
    }

    public bool TryParse<TResult>(Stream stream, out TResult result)
    {
        result = default!;
        return false;
    }
}

/// <summary>A skill database with no skills.</summary>
internal sealed class StubSkillDatabase : ISkillDatabase
{
    public Result<Skill> Get(string id) => Result<Skill>.FromFailure("No skills.");

    public IReadOnlyList<Skill> Get(XPSource xpSource) => [];
}

/// <summary>A modal page that renders nothing.</summary>
internal sealed class StubModalPage(WaywardBeyond.Client.UI.Layers.Menus.Modal.Modal id) : IMenuPage<WaywardBeyond.Client.UI.Layers.Menus.Modal.Modal>
{
    public WaywardBeyond.Client.UI.Layers.Menus.Modal.Modal ID => id;

    public Result RenderPage(double delta, Reef.UIBuilder<Graphics.Material> ui, Menu<WaywardBeyond.Client.UI.Layers.Menus.Modal.Modal> menu) => Result.FromSuccess();
}
