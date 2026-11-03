using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI;

/// <summary>Provides a shared localization helper to generated views.</summary>
internal abstract class UiViewBase(ILocalization localization)
{
    private const int COMPACT_WIDTH = 900;
    private const int STANDARD_WIDTH = 1020;
    private const int WIDE_WIDTH = 1220;
    private const int TALL_HEIGHT = 720;

    /// <summary>Gets whether the width uses the compact layout.</summary>
    protected static bool UsesCompactLayout(int width) => width >= COMPACT_WIDTH;

    /// <summary>Gets whether the width uses the standard layout.</summary>
    protected static bool UsesStandardLayout(int width) => width >= STANDARD_WIDTH;

    /// <summary>Gets whether the width uses the wide layout.</summary>
    protected static bool UsesWideLayout(int width) => width >= WIDE_WIDTH;

    /// <summary>Gets whether the height uses the tall layout.</summary>
    protected static bool UsesTallLayout(int height) => height >= TALL_HEIGHT;

    /// <summary>Returns a localized string, or the key when the string is missing.</summary>
    protected string L(string key) => localization.GetString(key) ?? key;
}
