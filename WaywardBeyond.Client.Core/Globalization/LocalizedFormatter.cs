using SmartFormat;
using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.Core.Globalization;

/// <summary>
///     A formatter that provides localized strings.
/// </summary>
internal class LocalizedFormatter(
    in ILocalization localization,
    in SmartFormatter smartFormatter
) {
    private readonly ILocalization _localization = localization;
    private readonly SmartFormatter _smartFormatter = smartFormatter;
    
    public string GetString(string key)
    {
        return _localization.GetString(key) ?? string.Empty;
    }
    
    public string GetString(string key, params object?[] args)
    {
        string localizedText = _localization.GetString(key) ?? string.Empty;
        return _smartFormatter.Format(localizedText, args);
    }
}