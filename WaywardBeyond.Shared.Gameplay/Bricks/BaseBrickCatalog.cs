using System.Collections.Generic;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// The shipped (non-mod) brick set, used as the deterministic bridge between voxel ids and brick names
/// that both the authoritative server and the client derive identically from the same content. Kept in
/// game-shared code so world generation, persistence, and migration never depend on a render-coupled
/// client database. The sorted registry over these names is the stable id space for base-game bricks;
/// mod bricks extend a per-load registry built from the same rule.
/// </summary>
public static class BaseBrickCatalog
{
    /// <summary>The base brick names shipped with the game, in no particular order.</summary>
    public static readonly IReadOnlyList<string> Names = new[]
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

    /// <summary>Registry over <see cref="Names"/>. Indices are stable for any given content set.</summary>
    public static BrickIdRegistry Registry { get; } = BrickIdRegistry.FromNames(Names);

    /// <summary>
    /// The brick name that a legacy raw FNV1a voxel id refers to, or null when no base brick hashes to it.
    /// </summary>
    public static string? LegacyNameFromDataId(ushort dataId)
    {
        return _legacyByDataId.TryGetValue(dataId, out string name) ? name : null;
    }

    private static readonly Dictionary<ushort, string> _legacyByDataId = BuildLegacyMap();

    private static Dictionary<ushort, string> BuildLegacyMap()
    {
        var map = new Dictionary<ushort, string>(Names.Count);
        foreach (string name in Names)
        {
            ushort id = FNV1a.ComputeDataID(name);
            if (!map.TryAdd(id, name))
            {
                //  A genuine FNV collision between two base bricks would be unrecoverable by id alone;
                //  the palette format makes such names unambiguous, so legacy saves degrade gracefully.
            }
        }

        return map;
    }
}