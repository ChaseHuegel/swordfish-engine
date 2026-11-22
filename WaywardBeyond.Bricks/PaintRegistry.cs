using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WaywardBeyond.Bricks;

/// <inheritdoc/>
public sealed class PaintRegistry : IPaintRegistry
{
    private readonly Dictionary<string, ushort> _idByName;
    private readonly Dictionary<ushort, string> _nameById;
    private readonly Lock _extensionLock = new();
    private readonly Dictionary<string, ushort> _extensionIdByName = new(StringComparer.Ordinal);
    private readonly List<string> _extensionNames = [];

    private PaintRegistry(Dictionary<string, ushort> idByName, Dictionary<ushort, string> nameById)
    {
        _idByName = idByName;
        _nameById = nameById;
    }

    /// <summary>Builds a registry over the supplied names, assigning sequential ids in ordinal name order.</summary>
    public static PaintRegistry FromNames(IEnumerable<string> names)
    {
        string[] sorted = [.. names.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal)];

        if (sorted.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException($"Too many paint ids: {sorted.Length} exceeds the reserved ushort space.");
        }

        var idByName = new Dictionary<string, ushort>(sorted.Length, StringComparer.Ordinal);
        var nameById = new Dictionary<ushort, string>(sorted.Length);
        for (var i = 0; i < sorted.Length; i++)
        {
            ushort id = (ushort)(i + Paint.MIN_ID);
            idByName[sorted[i]] = id;
            nameById[id] = sorted[i];
        }

        return new PaintRegistry(idByName, nameById);
    }

    /// <summary>The number of registered paint names in the registry.</summary>
    public int Count => _idByName.Count;

    /// <summary>The ids of every registered paint name in the registry.</summary>
    public IEnumerable<ushort> Ids => _nameById.Keys;

    /// <inheritdoc/>
    public ushort Id(string name)
    {
        return _idByName.GetValueOrDefault(name, (ushort)0);
    }

    /// <inheritdoc/>
    public ushort Resolve(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return 0;
        }

        if (_idByName.TryGetValue(name, out ushort knownId))
        {
            return knownId;
        }

        lock (_extensionLock)
        {
            if (_extensionIdByName.TryGetValue(name, out ushort existingId))
            {
                return existingId;
            }

            int nextId = _idByName.Count + _extensionNames.Count + Paint.MIN_ID;
            if (nextId > ushort.MaxValue)
            {
                throw new InvalidOperationException($"Too many paint ids: {nextId} exceeds the reserved ushort space.");
            }

            ushort allocated = (ushort)nextId;
            _extensionIdByName[name] = allocated;
            _extensionNames.Add(name);
            return allocated;
        }
    }

    /// <inheritdoc/>
    public string? Name(ushort id)
    {
        if (id < Paint.MIN_ID)
        {
            return null;
        }

        if (_nameById.TryGetValue(id, out string? knownName))
        {
            return knownName;
        }

        lock (_extensionLock)
        {
            int extensionIndex = id - Paint.MIN_ID - _idByName.Count;
            return extensionIndex >= 0 && extensionIndex < _extensionNames.Count ? _extensionNames[extensionIndex] : null;
        }
    }
}
