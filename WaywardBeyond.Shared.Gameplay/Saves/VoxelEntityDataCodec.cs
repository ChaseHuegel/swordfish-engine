using System;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// Normalizes the voxel id convention between a live voxel structure and its persisted/wire
/// <see cref="VoxelEntityData"/> at the save boundary. Since data version 4 a saved structure carries a
/// brick palette (<c>BrickPalette[n]</c> = the brick name for voxel id n) so saved voxel ids are stable
/// and self-describing regardless of the in-memory id scheme. Legacy (version 3) structures have no
/// palette and carry raw FNV1a brick ids directly; <see cref="EncodeToPalette"/> converts those, and
/// <see cref="DecodeToLocal"/> resolves a palette-carrying structure back to a caller's local id space.
/// </summary>
public static class VoxelEntityDataCodec
{
    /// <summary>
    /// Re-indexes a legacy (palette-less) structure into palette form: each voxel is translated from its
    /// raw FNV1a brick id to a stable registry id, and a <see cref="BaseBrickCatalog"/> name is recorded
    /// for that id in the palette. Ids not in the base catalog map to the empty voxel (0).
    /// </summary>
    public static VoxelEntityData EncodeToPalette(in VoxelEntityData source)
    {
        if (HasPalette(in source))
        {
            return source;
        }

        ChunkInfo[] chunks = RemapVoxels(source.Chunks, id => IdToRegistryId(id));
        string[]? palette = BuildPalette(chunks);
        return new VoxelEntityData(
            source.Uuid,
            source.X, source.Y, source.Z,
            source.OrientationX, source.OrientationY, source.OrientationZ, source.OrientationW,
            source.ScaleX, source.ScaleY, source.ScaleZ,
            chunks,
            palette
        );
    }

    /// <summary>
    /// Resolves a persisted structure into a caller's local voxel id space. Palette-carrying structures
    /// (version 4+) map each voxel through its palette name to <paramref name="nameToLocalId"/>; legacy
    /// structures (no palette) already carry local FNV1a ids and are returned unchanged.
    /// </summary>
    public static VoxelEntityData DecodeToLocal(in VoxelEntityData source, Func<string, ushort> nameToLocalId)
    {
        if (!HasPalette(in source))
        {
            return source;
        }

        string[] palette = source.BrickPalette!;
        ChunkInfo[] chunks = RemapVoxels(source.Chunks, id => PaletteIdToLocalId(id, palette, nameToLocalId));
        return new VoxelEntityData(
            source.Uuid,
            source.X, source.Y, source.Z,
            source.OrientationX, source.OrientationY, source.OrientationZ, source.OrientationW,
            source.ScaleX, source.ScaleY, source.ScaleZ,
            chunks,
            null
        );
    }

    /// <summary>Whether the structure carries a brick palette (i.e. was written as data version 4+).</summary>
    public static bool HasPalette(in VoxelEntityData data)
    {
        return data.BrickPalette != null && data.BrickPalette.Length > 0;
    }

    /// <summary>Local (FNV1a) id of a base brick name; the shared in-memory id scheme.</summary>
    public static ushort BaseNameToLocalId(string name)
    {
        return FNV1a.ComputeDataID(name);
    }

    private static ushort IdToRegistryId(ushort legacyId)
    {
        string? name = BaseBrickCatalog.LegacyNameFromDataId(legacyId);
        if (name == null)
        {
            return 0;
        }

        return BaseBrickCatalog.Registry.TryId(name, out ushort registryId) ? registryId : (ushort)0;
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

    private static string[]? BuildPalette(ChunkInfo[] source)
    {
        if (source == null)
        {
            return null;
        }

        //  One pass to find the highest registry id so the palette is sized to the used space.
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

                palette[id] = BaseBrickCatalog.Registry.Name(id);
            }
        }

        return palette;
    }
}