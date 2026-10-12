using System;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;

namespace WaywardBeyond.Gameplay.Saves;

/// <summary>
/// Normalizes the voxel id convention between a live voxel structure and its persisted/wire
/// <see cref="VoxelEntityData"/> at the save boundary. Since data version 4 a saved structure carries a
/// brick palette (<c>BrickPalette[n]</c> = the brick name for voxel id n) so saved voxel ids are stable
/// and self-describing regardless of the in-memory id scheme. In-memory voxel ids are registry ids from an
/// <see cref="IBrickRegistry"/>.
/// </summary>
public static class VoxelEntityDataCodec
{
    /// <summary>Encodes a brick palette from chunks whose voxel ids already come from <paramref name="map"/>.</summary>
    public static string[]? EncodePalette(in ChunkInfo[] chunkInfos, IBrickRegistry map)
    {
        return BuildPalette(chunkInfos, map.Name);
    }
    
    /// <summary>
    /// Attaches a brick palette to a live structure whose voxel ids already come from <paramref name="map"/>.
    /// Voxel ids are unchanged; the palette records a name for each present id so the structure persists
    /// self-describing identities.
    /// </summary>
    public static VoxelEntityData EncodeToPalette(in VoxelEntityData source, IBrickRegistry map)
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
    /// Resolves a persisted structure into a caller's local voxel id space. Palette-carrying structures
    /// (version 4+) map each voxel through its palette name to <paramref name="map"/>; structures without
    /// a palette already carry local ids and are returned unchanged.
    /// </summary>
    public static VoxelEntityData DecodeToLocal(in VoxelEntityData source, IBrickRegistry map)
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

    private static bool HasPalette(in VoxelEntityData data)
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
            for (var v = 0; v < voxels.Length; v++)
            {
                Voxel voxel = voxels[v];
                if (voxel.ID != 0)
                {
                    voxel.ID = remap(voxel.ID);
                }

                remapped[v] = voxel;
            }

            result[c] = new ChunkInfo(chunkInfo.OffsetX, chunkInfo.OffsetY, chunkInfo.OffsetZ, new Chunk(chunk.Size, remapped));
        }

        return result;
    }

    private static string[]? BuildPalette(ChunkInfo[] source, Func<ushort, string?> nameById)
    {
        //  One pass to find the highest id so the palette is sized to the used space.
        ushort maxId = 0;
        for (var c = 0; c < source.Length; c++)
        {
            Voxel[]? voxels = source[c].Chunk.Voxels;
            if (voxels == null)
            {
                continue;
            }

            for (var v = 0; v < voxels.Length; v++)
            {
                ushort id = voxels[v].ID;
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

        var palette = new string[maxId + 1];
        Array.Fill(palette, string.Empty);
        for (var c = 0; c < source.Length; c++)
        {
            Voxel[]? voxels = source[c].Chunk.Voxels;
            if (voxels == null)
            {
                continue;
            }

            for (var v = 0; v < voxels.Length; v++)
            {
                ushort id = voxels[v].ID;
                if (id == 0 || !string.IsNullOrEmpty(palette[id]))
                {
                    continue;
                }

                //  An unknown id resolves to the empty name (never a valid brick id), keeping
                //  serialization hole-free; the codegen serializer cannot represent null entries.
                palette[id] = nameById(id) ?? "";
            }
        }

        return palette;
    }
}