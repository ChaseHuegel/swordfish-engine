using System;
using System.Collections.Generic;
using System.Linq;

namespace WaywardBeyond.Bricks;

/// <inheritdoc/>
public sealed class BrickRegistry : IBrickRegistry
{
    private readonly Dictionary<string, ushort> _idByName;
    private readonly Dictionary<ushort, string> _nameById;

    private BrickRegistry(Dictionary<string, ushort> idByName, Dictionary<ushort, string> nameById)
    {
        _idByName = idByName;
        _nameById = nameById;
    }

    /// <summary>Builds a registry over the supplied names, assigning sequential ids in ordinal name order.</summary>
    public static BrickRegistry FromNames(IEnumerable<string> names)
    {
        string[] sorted = names
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (sorted.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException($"Too many brick ids: {sorted.Length} exceeds the reserved ushort space.");
        }

        var idByName = new Dictionary<string, ushort>(sorted.Length, StringComparer.Ordinal);
        var nameById = new Dictionary<ushort, string>(sorted.Length);
        for (var i = 0; i < sorted.Length; i++)
        {
            ushort id = (ushort)(i + Brick.MinID);
            idByName[sorted[i]] = id;
            nameById[id] = sorted[i];
        }

        return new BrickRegistry(idByName, nameById);
    }

    /// <summary>The number of names in the registry.</summary>
    public int Count => _idByName.Count;

    /// <summary>The ids of every name in the registry, in no particular order.</summary>
    public IEnumerable<ushort> Ids => _nameById.Keys;

    /// <summary>The voxel id for a brick name, or 0 (empty) when not registered.</summary>
    public ushort Id(string name)
    {
        return _idByName.GetValueOrDefault(name, (ushort)0);
    }

    /// <summary>Resolves a name to its voxel id, or false when the name is not registered.</summary>
    public bool TryId(string name, out ushort id)
    {
        return _idByName.TryGetValue(name, out id);
    }

    /// <summary>The registered name for a voxel id, or null when not registered.</summary>
    public string? Name(ushort id)
    {
        return _nameById.GetValueOrDefault(id);
    }
}