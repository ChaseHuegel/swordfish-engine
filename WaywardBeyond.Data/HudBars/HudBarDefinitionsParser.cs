using Swordfish.Library.IO;
using Tomlet;
using Tomlet.Models;

namespace WaywardBeyond.Data.HudBars;

/// <summary>Parses HUD bar TOML files supporting both <c>[[hud_bars]]</c> tables and flat single-bar documents.</summary>
public sealed class HudBarDefinitionsParser : IFileParser<HudBarDefinitions>
{
    /// <inheritdoc/>
    public string[] SupportedExtensions { get; } = [".toml"];

    /// <inheritdoc/>
    object IFileParser.Parse(PathInfo path) => Parse(path);

    /// <inheritdoc/>
    public HudBarDefinitions Parse(PathInfo file)
    {
        var result = new HudBarDefinitions();
        TomlDocument document = new TomlParser().Parse(file.ReadString());

        if (TomlTableReader.TryFindValue(document, "hud_bars", out TomlValue? arrayValue) && arrayValue != null)
        {
            if (arrayValue is not TomlArray array)
            {
                result.Entries.Add(new HudBarEntry(default, "Field \"hud_bars\" must be an array of tables."));
                return result;
            }

            for (var i = 0; i < array.Count; i++)
            {
                if (array[i] is not TomlTable rowTable)
                {
                    result.Entries.Add(new HudBarEntry(default, $"Entry {i} in \"hud_bars\" must be a table."));
                    continue;
                }

                result.Entries.Add(ParseEntry(rowTable));
            }

            return result;
        }

        if (document.Keys.Count > 0)
        {
            result.Entries.Add(ParseEntry(document));
        }

        return result;
    }

    private static HudBarEntry ParseEntry(TomlTable table)
    {
        if (!TomlTableReader.TryGetString(table, "id", out string? id, out string? error)
            || !TomlTableReader.TryGetString(table, "stat_id", out string? statId, out error)
            || !TomlTableReader.TryGetString(table, "max_stat_id", out string? maxStatId, out error)
            || !TomlTableReader.TryGetFloat(table, "default_max", out float? defaultMax, out error)
            || !TomlTableReader.TryGetInt(table, "icon_count", HudBarDefinition.DEFAULT_ICON_COUNT, out int iconCount, out error)
            || !TomlTableReader.TryGetString(table, "icon_full", out string? iconFull, out error)
            || !TomlTableReader.TryGetString(table, "icon_half", out string? iconHalf, out error)
            || !TomlTableReader.TryGetString(table, "icon_empty", out string? iconEmpty, out error)
            || !TomlTableReader.TryGetFloat(table, "warn_threshold", out float? warnThreshold, out error)
            || !TomlTableReader.TryGetFloat(table, "critical_threshold", out float? criticalThreshold, out error)
            || !TomlTableReader.TryGetInt(table, "order", 0, out int order, out error)
            || !TomlTableReader.TryGetBool(table, "hide_in_creative", false, out bool hideInCreative, out error)
            || !TomlTableReader.TryGetString(table, "color_tint", out string? colorTint, out error))
        {
            return new HudBarEntry(new HudBarDefinition { ID = id ?? string.Empty }, error);
        }

        var definition = new HudBarDefinition
        {
            ID = id ?? string.Empty,
            StatId = statId ?? string.Empty,
            MaxStatId = maxStatId,
            DefaultMax = defaultMax,
            IconCount = iconCount,
            IconFull = iconFull ?? string.Empty,
            IconHalf = iconHalf ?? string.Empty,
            IconEmpty = iconEmpty ?? string.Empty,
            WarnThreshold = warnThreshold ?? HudBarDefinition.DEFAULT_WARN_THRESHOLD,
            CriticalThreshold = criticalThreshold ?? HudBarDefinition.DEFAULT_CRITICAL_THRESHOLD,
            Order = order,
            HideInCreative = hideInCreative,
            ColorTint = colorTint,
        };

        return new HudBarEntry(definition, null);
    }
}
