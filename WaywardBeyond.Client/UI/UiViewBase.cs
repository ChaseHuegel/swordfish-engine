using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI;

/// <summary>Provides shared localization and glyph helpers to generated views.</summary>
internal abstract class UiViewBase(ILocalization localization)
{
    /// <summary>Returns a localized string, or the key when the string is missing.</summary>
    protected string L(string key) => localization.GetString(key) ?? key;

    /// <summary>Returns a game UI glyph by name.</summary>
    protected string Glyph(string name) => MenuGlyphs.Get(name);
}
