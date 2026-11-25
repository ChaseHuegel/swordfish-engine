using System.Globalization;
using System.Resources;

namespace WaywardBeyond.AssetTools.Localization;

/// <summary>
///     Resolves tool UI culture with OS default, settings override, English fallback, and debug pseudo-locale support.
/// </summary>
public static class ToolLocalization
{
    /// <summary>
    ///     Culture name for the debug-only pseudo-locale catalog.
    /// </summary>
    public const string PSEUDO_LOCALE_NAME = "qps-ploc";

    /// <summary>
    ///     Default fallback culture used when the requested culture has no translation catalog.
    /// </summary>
    public static CultureInfo FallbackCulture { get; } = CultureInfo.GetCultureInfo("en");

    /// <summary>
    ///     Resolves the active culture from an optional settings override, an OS culture, and the English fallback.
    /// </summary>
    public static CultureInfo ResolveCulture(string? cultureOverride, CultureInfo? osCulture = null)
    {
        if (!string.IsNullOrWhiteSpace(cultureOverride))
        {
            CultureInfo? overrideCulture = TryGetCulture(cultureOverride.Trim());
            if (overrideCulture != null && !string.IsNullOrEmpty(overrideCulture.Name))
            {
                return overrideCulture;
            }
        }

        CultureInfo candidate = osCulture ?? CultureInfo.CurrentUICulture;
        if (candidate == null || string.IsNullOrEmpty(candidate.Name))
        {
            return FallbackCulture;
        }

        return candidate;
    }

    /// <summary>
    ///     Applies the resolved culture to <see cref="Strings.Culture"/> and returns it.
    /// </summary>
    public static CultureInfo ApplyCulture(string? cultureOverride, CultureInfo? osCulture = null)
    {
        CultureInfo resolved = ResolveCulture(cultureOverride, osCulture);
        Strings.Culture = resolved;
        return resolved;
    }

    /// <summary>
    ///     Looks up a localized tool string by <c>Area.Element</c> key with automatic fallback to English.
    /// </summary>
    public static string GetString(string key, CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        CultureInfo targetCulture = culture ?? Strings.Culture ?? FallbackCulture;
        string? value = Strings.ResourceManager.GetString(key, targetCulture);
        if (value != null)
        {
            return value;
        }

        string? fallback = Strings.ResourceManager.GetString(key, FallbackCulture);
        return fallback ?? key;
    }

    /// <summary>
    ///     Returns true when the <c>qps-ploc</c> pseudo-locale satellite catalog is compiled and loadable.
    /// </summary>
    public static bool IsPseudoLocaleAvailable()
    {
        CultureInfo? pseudo = TryGetCulture(PSEUDO_LOCALE_NAME);
        if (pseudo == null)
        {
            return false;
        }

        ResourceSet? resourceSet = Strings.ResourceManager.GetResourceSet(pseudo, createIfNotExists: true, tryParents: false);
        return resourceSet != null;
    }

    private static CultureInfo? TryGetCulture(string cultureName)
    {
        try
        {
            return CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
