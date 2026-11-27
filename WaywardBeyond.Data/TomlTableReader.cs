using System;
using System.Collections.Generic;
using Tomlet.Models;

namespace WaywardBeyond.Data;

/// <summary>Shared case- and style-tolerant TOML table field readers for data definitions.</summary>
internal static class TomlTableReader
{
    /// <summary>Finds a TOML value by key name, matching snake_case and PascalCase case-insensitively.</summary>
    public static bool TryFindValue(TomlTable table, string key, out TomlValue? value)
    {
        if (table.ContainsKey(key))
        {
            value = table.GetValue(key);
            return true;
        }

        foreach (string candidate in table.Keys)
        {
            if (KeysMatch(candidate, key))
            {
                value = table.GetValue(candidate);
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>Reads an optional string field, reporting an error when the TOML value is not a string.</summary>
    public static bool TryGetString(TomlTable table, string key, out string? value, out string? error)
    {
        error = null;
        if (!TryFindValue(table, key, out TomlValue? raw) || raw == null)
        {
            value = null;
            return true;
        }

        if (raw is TomlString tomlString)
        {
            value = tomlString.Value;
            return true;
        }

        value = null;
        error = $"Field \"{key}\" must be a string.";
        return false;
    }

    /// <summary>Reads an optional integer field, falling back to <paramref name="defaultValue"/> when absent.</summary>
    public static bool TryGetInt(TomlTable table, string key, int defaultValue, out int value, out string? error)
    {
        error = null;
        if (!TryFindValue(table, key, out TomlValue? raw) || raw == null)
        {
            value = defaultValue;
            return true;
        }

        if (raw is TomlLong tomlLong)
        {
            if (tomlLong.Value < int.MinValue || tomlLong.Value > int.MaxValue)
            {
                value = defaultValue;
                error = $"Field \"{key}\" is out of 32-bit integer range.";
                return false;
            }

            value = (int)tomlLong.Value;
            return true;
        }

        value = defaultValue;
        error = $"Field \"{key}\" must be an integer.";
        return false;
    }

    /// <summary>Reads an optional floating-point field, accepting both TOML floats and integers.</summary>
    public static bool TryGetFloat(TomlTable table, string key, out float? value, out string? error)
    {
        error = null;
        if (!TryFindValue(table, key, out TomlValue? raw) || raw == null)
        {
            value = null;
            return true;
        }

        if (raw is TomlDouble tomlDouble)
        {
            value = (float)tomlDouble.Value;
            return true;
        }

        if (raw is TomlLong tomlLong)
        {
            value = tomlLong.Value;
            return true;
        }

        value = null;
        error = $"Field \"{key}\" must be a number.";
        return false;
    }

    /// <summary>Reads an optional boolean field, falling back to <paramref name="defaultValue"/> when absent.</summary>
    public static bool TryGetBool(TomlTable table, string key, bool defaultValue, out bool value, out string? error)
    {
        error = null;
        if (!TryFindValue(table, key, out TomlValue? raw) || raw == null)
        {
            value = defaultValue;
            return true;
        }

        if (raw is TomlBoolean tomlBool)
        {
            value = tomlBool.Value;
            return true;
        }

        value = defaultValue;
        error = $"Field \"{key}\" must be a boolean.";
        return false;
    }

    /// <summary>Reads an optional nested TOML table.</summary>
    public static bool TryGetTable(TomlTable table, string key, out TomlTable? child, out string? error)
    {
        error = null;
        if (!TryFindValue(table, key, out TomlValue? raw) || raw == null)
        {
            child = null;
            return true;
        }

        if (raw is TomlTable tomlTable)
        {
            child = tomlTable;
            return true;
        }

        child = null;
        error = $"Field \"{key}\" must be a table.";
        return false;
    }

    /// <summary>Reads an optional TOML array of tables.</summary>
    public static bool TryGetTableArray(TomlTable table, string key, out List<TomlTable>? items, out string? error)
    {
        error = null;
        if (!TryFindValue(table, key, out TomlValue? raw) || raw == null)
        {
            items = null;
            return true;
        }

        if (raw is not TomlArray array)
        {
            items = null;
            error = $"Field \"{key}\" must be an array of tables.";
            return false;
        }

        var result = new List<TomlTable>(array.Count);
        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not TomlTable itemTable)
            {
                items = null;
                error = $"Entry {i} in \"{key}\" must be a table.";
                return false;
            }

            result.Add(itemTable);
        }

        items = result;
        return true;
    }

    private static bool KeysMatch(string candidate, string expected)
    {
        if (string.Equals(candidate, expected, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ci = 0;
        var ei = 0;
        while (ci < candidate.Length || ei < expected.Length)
        {
            while (ci < candidate.Length && candidate[ci] == '_')
            {
                ci++;
            }

            while (ei < expected.Length && expected[ei] == '_')
            {
                ei++;
            }

            if (ci >= candidate.Length || ei >= expected.Length)
            {
                return ci >= candidate.Length && ei >= expected.Length;
            }

            if (char.ToUpperInvariant(candidate[ci]) != char.ToUpperInvariant(expected[ei]))
            {
                return false;
            }

            ci++;
            ei++;
        }

        return true;
    }
}
