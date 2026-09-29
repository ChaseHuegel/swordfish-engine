using System;
using System.Collections.Generic;
using WaywardBeyond.Shared.Bricks;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// Normalizes the voxel id convention between a live voxel structure and its persisted/wire
/// <see cref="VoxelEntityData"/> at the save boundary. Since data version 4 a saved structure carries a
/// brick palette (<c>BrickPalette[n]</c> = the brick name for voxel id n) so saved voxel ids are stable
/// and self-describing regardless of the in-memory id scheme. In-memory voxel ids are registry ids from an
/// <see cref="IBrickIdMap"/>; version 3 structures had no palette and carried raw FNV1a brick ids, which
/// the <c>VoxelEntityDataV3ToV4Migration</c> re-indexes.
/// </summary>
public static class VoxelEntityDataCodec
{
    /// <summary>
    /// Attaches a brick palette to a live structure whose voxel ids already come from <paramref name="map"/>.
    /// Voxel ids are unchanged; the palette records a name for each present id so the structure persists
    /// self-describing identities.
    /// </summary>
    public static VoxelEntityData EncodeToPalette(in VoxelEntityData source, IBrickIdMap map)
    {
        if (HasPalette(in source))
        {
            return source;
        }

        string[]? palette = BuildPalette(source.Chunks, map.Name);
        return new VoxelEntityData(
            source.Uuid,
            source.X, source.Y, source.Z,
            source.OrientationX, source.OrientationY, source.OrientationZ, source.OrientationW,
            source.ScaleX, source.ScaleY, source.ScaleZ,
            source.Chunks,
            palette
        );
    }

    /// <summary>
/// Re-indexes a legacy (version 3) structure into palette form, translating raw FNV1a brick ids that
/// were hashed from the bare (un-namespaced) name. <paramref name="legacyIdToName"/> resolves a legacy
/// id to its current namespaced name; the caller (the v3 to v4 migration) supplies it because those
/// legacy names are migration data. The structure is re-id'd over a registry built from its own names.
/// </summary>
public static VoxelEntityData EncodeLegacyToPalette(in VoxelEntityData source, Func<ushort, string?> legacyIdToName)
    {
        if (HasPalette(in source))
        {
            return source;
        }

        //  Collect the distinct migrated names so a local, data-driven registry gives the structure a
        //  self-consistent palette-id space that DecodeToLocal later maps through the current map.
        var names = new List<string>();
        foreach (ChunkInfo chunkInfo in source.Chunks)
        {
            Voxel[]? voxels = chunkInfo.Chunk.Voxels;
            if (voxels == null)
            {
                continue;
            }

            for (var i = 0; i < voxels.Length; i++)
            {
                string? name = LegacyNameOf(voxels[i].ID, legacyIdToName);
                if (name != null && !names.Contains(name))
                {
                    names.Add(name);
                }
            }
        }

        BrickIdRegistry local = BrickIdRegistry.FromNames(names);
        ChunkInfo[] chunks = RemapVoxels(source.Chunks, id => LegacyIdToLocalId(id, legacyIdToName, local));
        string[]? palette = BuildPalette(chunks, local.Name);
        return new VoxelEntityData(
            source.Uuid,
            source.X, source.Y, source.Z,
            source.OrientationX, source.OrientationY, source.OrientationZ, source.OrientationW,
            source.ScaleX, source.ScaleY, source.ScaleZ,
            chunks,
            palette
        );
    }

    private static string? LegacyNameOf(ushort legacyId, Func<ushort, string?> legacyIdToName)
    {
        return legacyId == 0 ? null : legacyIdToName(legacyId);
    }

    private static ushort LegacyIdToLocalId(ushort legacyId, Func<ushort, string?> legacyIdToName, BrickIdRegistry local)
    {
        string? name = LegacyNameOf(legacyId, legacyIdToName);
        return name == null ? (ushort)0 : local.Id(name);
    }

    /// <summary>
    /// Resolves a persisted structure into a caller's local voxel id space. Palette-carrying structures
    /// (version 4+) map each voxel through its palette name to <paramref name="map"/>; structures without
    /// a palette already carry local ids and are returned unchanged.
    /// </summary>
    public static VoxelEntityData DecodeToLocal(in VoxelEntityData source, IBrickIdMap map)
    {
        if (!HasPalette(in source))
        {
            return source;
        }

        string[] palette = source.BrickPalette!;
        ChunkInfo[] chunks = RemapVoxels(source.Chunks, id => PaletteIdToLocalId(id, palette, map.Id));
        return new VoxelEntityData(
            source.Uuid,
            source.X, source.Y, source.Z,
            source.OrientationX, source.OrientationY, source.OrientationZ, source.OrientationW,
            source.ScaleX, source.ScaleY, source.ScaleZ,
            chunks,
            null
        );
    }

    /// <summary>Returns whether the structure carries a brick palette (i.e. was written as version 4+).</summary>
    public static bool HasPalette(in VoxelEntityData data)
    {
        return data.BrickPalette != null && data.BrickPalette.Length > 0;
    }

    private static ushort PaletteIdToLocalId(ushort paletteIndex, string[] palette, Func<string, ushort> nameToLocalId)
    {
        if (paletteIndex == 0 || paletteIndex >= palette.Length)
        {
            return 0;
        }

        string name = palette[paletteIndex];
        return string.IsNullOrEmpty(name) ? (ushort)0 : nameToLocalId(name);
    }

    private static ChunkInfo[] RemapVoxels(ChunkInfo[] source, Func<ushort, ushort> remap)
    {
        if (source == null)
        {
            return source;
        }

        var result = new ChunkInfo[source.Length];
        for (var c = 0; c < source.Length; c++)
        {
            ChunkInfo chunkInfo = source[c];
            Chunk chunk = chunkInfo.Chunk;
            Voxel[] voxels = chunk.Voxels;
            if (chunk.Size == 0 || voxels == null)
            {
                result[c] = chunkInfo;
                continue;
            }

            var remapped = new Voxel[voxels.Length];
            for (var i = 0; i < voxels.Length; i++)
            {
                Voxel voxel = voxels[i];
                if (voxel.ID != 0)
                {
                    voxel.ID = remap(voxel.ID);
                }

                remapped[i] = voxel;
            }

            result[c] = new ChunkInfo(chunkInfo.OffsetX, chunkInfo.OffsetY, chunkInfo.OffsetZ, new Chunk(chunk.Size, remapped));
        }

        return result;
    }

    private static string[]? BuildPalette(ChunkInfo[] source, Func<ushort, string?> nameById)
    {
        if (source == null)
        {
            return null;
        }

        //  One pass to find the highest id so the palette is sized to the used space.
        ushort maxId = 0;
        for (var c = 0; c < source.Length; c++)
        {
            Voxel[]? voxels = source[c].Chunk.Voxels;
            if (voxels == null)
            {
                continue;
            }

            for (var i = 0; i < voxels.Length; i++)
            {
                ushort id = voxels[i].ID;
                if (id != 0 && id > maxId)
                {
                    maxId = id;
                }
            }
        }

        if (maxId == 0)
        {
            return null;
        }

        //  Unused palette slots hold the empty string (never a valid brick name) so serialization stays
        //  hole-free; the codegen serializer cannot represent null entries.
        var palette = new string[maxId + 1];
        Array.Fill(palette, "");
        for (var c = 0; c < source.Length; c++)
        {
            Voxel[]? voxels = source[c].Chunk.Voxels;
            if (voxels == null)
            {
                continue;
            }

            for (var i = 0; i < voxels.Length; i++)
            {
                ushort id = voxels[i].ID;
                if (id == 0 || !string.IsNullOrEmpty(palette[id]))
                {
                    continue;
                }

                palette[id] = nameById(id);
            }
        }

        return palette;
    }
}