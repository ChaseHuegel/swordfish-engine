using System.Collections.Generic;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Bricks;

/// <summary>The shipped (non-mod) brick set, used as the deterministic bridge between voxel ids and brick names
/// that both the authoritative server and the client derive identically from the same content. Kept in
/// game-shared code so world generation, persistence, and migration never depend on a render-coupled
/// client database. Base-brick names are namespaced (<see cref="Namespace"/>) so no mod brick name can
/// shadow them; the registry over these names is the stable base id space.
/// </summary>
public static class BaseBrickCatalog
{
    /// <summary>Namespace prefix for shipped (base-game) brick and item ids.</summary>
    public const string Namespace = "wb";

    /// <summary>Returns a brick id namespaced under <see cref="Namespace"/>.</summary>
    public static string Namespaced(string bareId)
    {
        return $"{Namespace}:{bareId}";
    }

    /// <summary>The namespaced base brick names shipped with the game, in bare (unprefixed) order.</summary>
    public static readonly IReadOnlyList<string> BareNames = new[]
    {
        "caution_panel",
        "control_buttons",
        "control_panel",
        "core",
        "display_console",
        "display_control",
        "display_monitor",
        "glass",
        "grate",
        "ice",
        "light",
        "panel",
        "porthole",
        "rock",
        "small_light",
        "storage",
        "thruster",
        "truss",
        "vent",
    };

    /// <summary>The namespaced base brick names as they appear in content and the live registry.</summary>
    public static readonly IReadOnlyList<string> Names = BuildNamespaced(BareNames);

    /// <summary>Registry over <see cref="Names"/>. Indices are stable for any given content set.</summary>
    public static BrickIdRegistry Registry { get; } = BrickIdRegistry.FromNames(Names);

    /// <summary>
    /// The brick name (namespaced) that a legacy raw FNV1a voxel id (hashed from the bare name) refers to,
    /// or null when no base brick hashes to it.
    /// </summary>
    public static string? LegacyNameFromDataId(ushort dataId)
    {
        return _legacyByDataId.TryGetValue(dataId, out string name) ? name : null;
    }

    private static readonly Dictionary<ushort, string> _legacyByDataId = BuildLegacyMap();

    private static IReadOnlyList<string> BuildNamespaced(IReadOnlyList<string> bare)
    {
        var names = new string[bare.Count];
        for (var i = 0; i < bare.Count; i++)
        {
            names[i] = Namespaced(bare[i]);
        }

        return names;
    }

    private static Dictionary<ushort, string> BuildLegacyMap()
    {
        //  Legacy (data version 3) saves stored FNV1a ids of the BARE name; a v4 palette must carry the
        //  current namespaced name, so the reverse map translates FNV(bare) to the namespaced identity.
        var map = new Dictionary<ushort, string>(BareNames.Count);
        foreach (string bare in BareNames)
        {
            ushort id = FNV1a.ComputeDataID(bare);
            if (!map.TryAdd(id, Namespaced(bare)))
            {
                //  A genuine FNV collision between two base bricks would be unrecoverable by id alone;
                //  the palette format makes such names unambiguous, so legacy saves degrade gracefully.
            }
        }

        return map;
    }
}