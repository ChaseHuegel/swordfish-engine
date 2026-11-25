using System;
using System.Collections;
using System.Collections.Generic;

namespace WaywardBeyond.Data.Localization;

/// <summary>A tag's values, unique by ordinal comparison and ordered by first occurrence.</summary>
internal sealed class LocalizedTagValues : IReadOnlyList<string>
{
    private readonly List<string> _ordered = [];
    private readonly HashSet<string> _values = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public string this[int index] => _ordered[index];

    /// <inheritdoc/>
    public int Count => _ordered.Count;

    /// <summary>Adds a value when it is not already present.</summary>
    public bool Add(string value)
    {
        if (!_values.Add(value))
        {
            return false;
        }

        _ordered.Add(value);
        return true;
    }

    /// <summary>Returns whether the values contain the specified value.</summary>
    public bool Contains(string value) => _values.Contains(value);

    /// <inheritdoc/>
    public IEnumerator<string> GetEnumerator() => _ordered.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
