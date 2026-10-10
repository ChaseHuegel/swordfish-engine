using System;
using System.Collections.Generic;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;

namespace WaywardBeyond.Gameplay;

/// <summary>
/// Data version 3 to 4 migration for world voxel structures. Version 3 written structures carry raw
/// FNV1a voxel ids and no brick palette; version 4 carries a brick palette so saved ids are stable and
/// self-describing. The legacy bare-name reverse map is the only hardcoded brick data, and it is private
/// to this migration on purpose (see <see cref="LegacyNames"/>).
/// </summary>
internal sealed class VoxelEntityDataV3ToV4Migration : SaveMigration<VoxelEntityData>
{
    public override uint FromVersion => 3;
    public override uint ToVersion => 4;

    public override VoxelEntityData ApplyValue(VoxelEntityData value)
    {
        return VoxelEntityDataCodec.EncodeLegacyToPalette(in value, LegacyNames.LegacyNameFromDataId);
    }

    /// <summary>
    /// Legacy (data version 3) saves stored FNV1a ids of the BARE brick name; the v4 palette must carry
    /// the current namespaced name. This reverse map is the only hardcoded brick data in the codebase,
    /// and it is migration-only so it never leaks into the runtime brick id API.
    /// </summary>
    private static class LegacyNames
    {
        private static readonly string[] _bareNames =
        [
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
        ];

        private static readonly Dictionary<ushort, string> _byDataId = Build();

        private static Dictionary<ushort, string> Build()
        {
            var map = new Dictionary<ushort, string>(_bareNames.Length);
            foreach (string bare in _bareNames)
            {
                ushort id = FNV1a.ComputeDataID(bare);
                map.TryAdd(id, $"wb:{bare}");
            }
            return map;
        }

        public static string? LegacyNameFromDataId(ushort dataId)
        {
            return _byDataId.TryGetValue(dataId, out string name) ? name : null;
        }
    }
}

/// <summary>
/// The standard data-format migrator for game-save records. Registers every forward migration shipping
/// with this build. Provides the single <see cref="SaveMigrator"/> used by the persistence paths.
/// </summary>
public static class GameSaveMigrations
{
    /// <summary>A migrator carrying every game-save record migration for this build.</summary>
    public static SaveMigrator Migrator { get; } = new([new VoxelEntityDataV3ToV4Migration()]);
}