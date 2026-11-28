using System;
using System.Collections.Generic;
using Swordfish.Library.IO;
using Tomlet;
using Tomlet.Models;

namespace WaywardBeyond.Data.Buffs;

/// <summary>Parses buff TOML files supporting both <c>[[buffs]]</c> tables and flat single-buff documents.</summary>
internal sealed class BuffDefinitionsParser : IFileParser<BuffDefinitions>
{
    /// <inheritdoc/>
    public string[] SupportedExtensions { get; } = [".toml"];

    /// <inheritdoc/>
    object IFileParser.Parse(PathInfo path) => Parse(path);

    /// <inheritdoc/>
    public BuffDefinitions Parse(PathInfo file)
    {
        var result = new BuffDefinitions();
        TomlDocument document = new TomlParser().Parse(file.ReadString());
        List<(TomlTable? Table, string? Error)> rows = TomlTableReader.ReadDocumentRows(document, "buffs");

        for (var i = 0; i < rows.Count; i++)
        {
            (TomlTable? rowTable, string? rowError) = rows[i];
            result.Entries.Add(rowTable != null ? ParseEntry(rowTable) : new BuffEntry(default, rowError));
        }

        return result;
    }

    private static BuffEntry ParseEntry(TomlTable table)
    {
        if (!TomlTableReader.TryGetString(table, "id", out string? id, out string? error)
            || !TomlTableReader.TryGetString(table, "name", out string? name, out error)
            || !TomlTableReader.TryGetString(table, "description", out string? description, out error)
            || !TomlTableReader.TryGetString(table, "icon", out string? icon, out error)
            || !TomlTableReader.TryGetFloat(table, "duration_seconds", out float? durationSeconds, out error)
            || !TomlTableReader.TryGetString(table, "stack_policy", out string? stackPolicyRaw, out error))
        {
            return new BuffEntry(new BuffDefinition { ID = id ?? string.Empty }, error);
        }

        if (!TryParseStackPolicy(stackPolicyRaw, out BuffStackPolicy stackPolicy, out error))
        {
            return new BuffEntry(new BuffDefinition { ID = id ?? string.Empty }, error);
        }

        if (!ParseModifiers(table, out BuffStatModifier[] modifiers, out error))
        {
            return new BuffEntry(new BuffDefinition { ID = id ?? string.Empty }, error);
        }

        if (!ParseRegenRates(table, out float healthPerSec, out float oxygenPerSec, out error))
        {
            return new BuffEntry(new BuffDefinition { ID = id ?? string.Empty }, error);
        }

        var definition = new BuffDefinition
        {
            ID = id ?? string.Empty,
            Name = name ?? string.Empty,
            Description = description,
            Icon = icon,
            DurationSeconds = durationSeconds ?? 0f,
            StackPolicy = stackPolicy,
            StatModifiers = modifiers,
            HealthPerSec = healthPerSec,
            OxygenPerSec = oxygenPerSec,
            ExpiryStatId = string.Empty,
        };

        return new BuffEntry(definition, null);
    }

    private static bool TryParseStackPolicy(string? raw, out BuffStackPolicy policy, out string? error)
    {
        error = null;
        if (raw == null)
        {
            policy = BuffStackPolicy.Refresh;
            return true;
        }

        ReadOnlySpan<char> trimmed = raw.AsSpan().Trim();
        if (trimmed.Equals("refresh".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            policy = BuffStackPolicy.Refresh;
            return true;
        }

        if (trimmed.Equals("extend".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            policy = BuffStackPolicy.Extend;
            return true;
        }

        if (trimmed.Equals("ignore".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            policy = BuffStackPolicy.Ignore;
            return true;
        }

        policy = BuffStackPolicy.Refresh;
        error = $"Invalid stack_policy \"{raw}\"; expected refresh, extend, or ignore.";
        return false;
    }

    private static bool ParseModifiers(TomlTable table, out BuffStatModifier[] modifiers, out string? error)
    {
        if (!TomlTableReader.TryGetTableArray(table, "stat_modifiers", out List<TomlTable>? rows, out error))
        {
            modifiers = [];
            return false;
        }

        if (rows == null || rows.Count == 0)
        {
            modifiers = [];
            return true;
        }

        modifiers = new BuffStatModifier[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            TomlTable row = rows[i];
            if (!TomlTableReader.TryGetString(row, "stat_id", out string? statId, out error)
                || !TomlTableReader.TryGetFloat(row, "flat_delta", out float? flatDelta, out error)
                || !TomlTableReader.TryGetFloat(row, "percent_delta", out float? percentDelta, out error))
            {
                modifiers = [];
                return false;
            }

            modifiers[i] = new BuffStatModifier(statId ?? string.Empty, flatDelta, percentDelta);
        }

        return true;
    }

    private static bool ParseRegenRates(
        TomlTable table,
        out float healthPerSec,
        out float oxygenPerSec,
        out string? error
    )
    {
        healthPerSec = 0f;
        oxygenPerSec = 0f;

        if (!TomlTableReader.TryGetFloat(table, "health_per_sec", out float? topHealth, out error)
            || !TomlTableReader.TryGetFloat(table, "oxygen_per_sec", out float? topOxygen, out error))
        {
            return false;
        }

        if (!TomlTableReader.TryGetTable(table, "regen_rates", out TomlTable? regenRatesTable, out error)
            || !TomlTableReader.TryGetTable(table, "regen", out TomlTable? regenShortTable, out error))
        {
            return false;
        }

        if (regenRatesTable != null && regenShortTable != null)
        {
            error = "Buff cannot define both [regen] and [regen_rates] sub-tables.";
            return false;
        }

        TomlTable? regenTable = regenRatesTable ?? regenShortTable;
        if (regenTable != null)
        {
            if (topHealth.HasValue || topOxygen.HasValue)
            {
                error = "Buff cannot define both top-level regen rates and a regen sub-table.";
                return false;
            }

            if (!TomlTableReader.TryGetFloat(regenTable, "health_per_sec", out topHealth, out error)
                || !TomlTableReader.TryGetFloat(regenTable, "oxygen_per_sec", out topOxygen, out error))
            {
                return false;
            }
        }

        healthPerSec = topHealth ?? 0f;
        oxygenPerSec = topOxygen ?? 0f;
        return true;
    }
}
