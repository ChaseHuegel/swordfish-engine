using System;
using System.Collections.Generic;
using System.Linq;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Bricks;

/// <summary>
/// A deterministic, collision-free mapping from brick names to 16-bit voxel ids for a single load of
/// content. Ids are assigned by sorting the supplied name set (ordinal, culture-invariant) so the
/// outcome depends only on the set of names present, never on load order. Id 0 is reserved as the
/// empty/air voxel, so assigned ids begin at <see cref="SaveVersion.MinDataId"/>.
/// </summary>
/// <remarks>
/// Assigned ids are transient for this content load. Persisted world data carries a brick palette
/// (index to name) so stored ids can be remapped to whatever registry a later run builds; the registry
/// itself is never the durable identity. The canonical identity is the string name.
/// </remarks>
public sealed class BrickIdRegistry : IBrickIdMap
{
    private readonly Dictionary<string, ushort> _idByName;
    private readonly Dictionary<ushort, string> _nameById;

    private BrickIdRegistry(Dictionary<string, ushort> idByName, Dictionary<ushort, string> nameById)
    {
        _idByName = idByName;
        _nameById = nameById;
    }

    /// <summary>
    /// Builds a registry over the supplied names, assigning sequential ids in ordinal name order.
    /// </summary>
    public static BrickIdRegistry FromNames(IEnumerable<string> names)
    {
        string[] sorted = names
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var idByName = new Dictionary<string, ushort>(sorted.Length, StringComparer.Ordinal);
        var nameById = new Dictionary<ushort, string>(sorted.Length);
        for (var i = 0; i < sorted.Length; i++)
        {
            //  Id 0 is the empty voxel; sequential ids start after it.
            ushort id = (ushort)(SaveVersion.MinDataId + i);
            idByName[sorted[i]] = id;
            nameById[id] = sorted[i];
        }

        return new BrickIdRegistry(idByName, nameById);
    }

    /// <summary>
    /// Extends a base registry with extra names, preserving every base id so a brick keeps the same id
    /// whether or not the extra (typically mod) names are present. Extra names are appended after all
    /// base names, sorted ordinal, so base ids are immutable across content changes.
    /// </summary>
    public static BrickIdRegistry FromBaseAndExtras(BrickIdRegistry baseRegistry, IEnumerable<string> extraNames)
    {
        var idByName = new Dictionary<string, ushort>(baseRegistry._idByName, StringComparer.Ordinal);
        var nameById = new Dictionary<ushort, string>(baseRegistry._nameById);

        ushort nextId = (ushort)(SaveVersion.MinDataId + baseRegistry.Count);
        foreach (string name in extraNames
                     .Where(name => !idByName.ContainsKey(name))
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            //  Skip any id already taken by a base name to keep ids unique and contiguous after them.
            while (nameById.ContainsKey(nextId))
            {
                nextId++;
            }

            idByName[name] = nextId;
            nameById[nextId] = name;
            nextId++;
        }

        return new BrickIdRegistry(idByName, nameById);
    }

    /// <summary>The number of names in the registry.</summary>
    public int Count => _idByName.Count;

    /// <summary>The ids of every name in the registry, in no particular order.</summary>
    public IEnumerable<ushort> Ids => _nameById.Keys;

    /// <summary>The voxel id for a brick name, or 0 (empty) when not registered.</summary>
    public ushort Id(string name)
    {
        return _idByName.TryGetValue(name, out ushort id) ? id : (ushort)0;
    }

    /// <summary>Resolves a name to its voxel id, or false when the name is not registered.</summary>
    public bool TryId(string name, out ushort id)
    {
        return _idByName.TryGetValue(name, out id);
    }

    /// <summary>Resolves a voxel id to its name, or false when the id is not registered.</summary>
    public bool TryName(ushort id, out string name)
    {
        if (_nameById.TryGetValue(id, out string? found))
        {
            name = found;
            return true;
        }

        name = string.Empty;
        return false;
    }

    /// <summary>The registered name for a voxel id, or null when not registered.</summary>
    public string? Name(ushort id)
    {
        return _nameById.TryGetValue(id, out string? found) ? found : null;
    }
}