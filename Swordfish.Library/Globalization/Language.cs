using System.Collections.Generic;
using Swordfish.Library.Configuration;
using Tomlet.Attributes;

namespace Swordfish.Library.Globalization;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class Language(string twoLetterISOLanguageName) : TomlConfiguration<Language>
{
    public readonly Dictionary<string, string> Translations = [];

    [TomlProperty("Language")]
    public string TwoLetterISOLanguageName { get; private set; } = twoLetterISOLanguageName;
}