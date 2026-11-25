using System;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;

namespace WaywardBeyond.Voxels;

/// <summary>Normalizes voxel data between the local transient format (in-memory) and the persistent format (wire/disk).</summary>
public static class VoxelCodec
{
    /// <summary>Encodes voxel data from the local format into the persistent format.</summary>
    public static VoxelEntityData Encode(in VoxelEntityData source, IBrickRegistry map, IPaintRegistry? paintMap = null)
    {
        bool hasBrickPalette = HasPalette(source.BrickPalette);
        bool hasPaintPalette = HasPalette(source.PaintPalette);
        if (hasBrickPalette && (paintMap == null || hasPaintPalette))
        {
            return source;
        }

        string[]? brickPalette = hasBrickPalette ? source.BrickPalette : BuildBrickPalette(source.Chunks, map.Name);
        string[]? paintPalette = hasPaintPalette
            ? source.PaintPalette
            : (paintMap != null ? BuildPaintPalette(source.Chunks, paintMap.Name) : null);

        return new VoxelEntityData(
            source.Uuid,
            source.X, source.Y, source.Z,
            source.OrientationX, source.OrientationY, source.OrientationZ, source.OrientationW,
            source.ScaleX, source.ScaleY, source.ScaleZ,
            source.Chunks,
            brickPalette,
            paintPalette
        );
    }

    /// <summary>Decodes voxel data from the persistent format into the local format.</summary>
    public static VoxelEntityData Decode(in VoxelEntityData source, IBrickRegistry map, IPaintRegistry? paintMap = null)
    {
        bool hasBrickPalette = HasPalette(source.BrickPalette);
        bool hasPaintPalette = HasPalette(source.PaintPalette);
        bool decodePaint = hasPaintPalette && paintMap != null;
        if (!hasBrickPalette && !decodePaint)
        {
            return source;
        }

        string[]? brickPalette = hasBrickPalette ? source.BrickPalette : null;
        string[]? paintPalette = decodePaint ? source.PaintPalette : null;

        Func<ushort, ushort>? remapBrick = brickPalette != null
            ? id => PaletteIdToLocalId(id, brickPalette, map.Resolve)
            : null;
        Func<ushort, ushort>? remapPaint = paintPalette != null
            ? id => PaletteIdToLocalId(id, paintPalette, paintMap!.Resolve)
            : null;

        ChunkInfo[] chunks = RemapChunks(source.Chunks, remapBrick, remapPaint);
        return new VoxelEntityData(
            source.Uuid,
            source.X, source.Y, source.Z,
            source.OrientationX, source.OrientationY, source.OrientationZ, source.OrientationW,
            source.ScaleX, source.ScaleY, source.ScaleZ,
            chunks,
            null,
            decodePaint ? null : source.PaintPalette
        );
    }

    /// <summary>Encodes voxel region data from the local format into the persistent format.</summary>
    public static VoxelRegionData Encode(in VoxelRegionData source, IBrickRegistry map, IPaintRegistry? paintMap = null)
    {
        bool hasBrickPalette = HasPalette(source.BrickPalette);
        bool hasPaintPalette = HasPalette(source.PaintPalette);
        if (hasBrickPalette && (paintMap == null || hasPaintPalette))
        {
            return source;
        }

        string[]? brickPalette = hasBrickPalette ? source.BrickPalette : BuildBrickPalette(source.Chunks, map.Name);
        string[]? paintPalette = hasPaintPalette
            ? source.PaintPalette
            : (paintMap != null ? BuildPaintPalette(source.Chunks, paintMap.Name) : null);

        return new VoxelRegionData(
            source.SizeX,
            source.SizeY,
            source.SizeZ,
            source.Chunks,
            brickPalette,
            paintPalette
        );
    }

    /// <summary>Decodes voxel region data from the persistent format into the local format.</summary>
    public static VoxelRegionData Decode(in VoxelRegionData source, IBrickRegistry map, IPaintRegistry? paintMap = null)
    {
        bool hasBrickPalette = HasPalette(source.BrickPalette);
        bool hasPaintPalette = HasPalette(source.PaintPalette);
        bool decodePaint = hasPaintPalette && paintMap != null;
        if (!hasBrickPalette && !decodePaint)
        {
            return source;
        }

        string[]? brickPalette = hasBrickPalette ? source.BrickPalette : null;
        string[]? paintPalette = decodePaint ? source.PaintPalette : null;

        Func<ushort, ushort>? remapBrick = brickPalette != null
            ? id => PaletteIdToLocalId(id, brickPalette, map.Resolve)
            : null;
        Func<ushort, ushort>? remapPaint = paintPalette != null
            ? id => PaletteIdToLocalId(id, paintPalette, paintMap!.Resolve)
            : null;

        ChunkInfo[] chunks = RemapChunks(source.Chunks, remapBrick, remapPaint);
        return new VoxelRegionData(
            source.SizeX,
            source.SizeY,
            source.SizeZ,
            chunks,
            null,
            decodePaint ? null : source.PaintPalette
        );
    }

    private static bool HasPalette(string[]? palette)
    {
        return palette != null && palette.Length > 0;
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

    private static ChunkInfo[] RemapChunks(
        ChunkInfo[]? source,
        Func<ushort, ushort>? remapBrick,
        Func<ushort, ushort>? remapPaint
    ) {
        if (source == null || source.Length == 0)
        {
            return [];
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

            Voxel[] remappedVoxels = voxels;
            if (remapBrick != null)
            {
                remappedVoxels = new Voxel[voxels.Length];
                for (var v = 0; v < voxels.Length; v++)
                {
                    Voxel voxel = voxels[v];
                    if (voxel.ID != 0)
                    {
                        voxel.ID = remapBrick(voxel.ID);
                    }

                    remappedVoxels[v] = voxel;
                }
            }

            FaceOverrideData[]? remappedOverrides = chunk.FaceOverrides;
            if (remapPaint != null && chunk.FaceOverrides != null && chunk.FaceOverrides.Length > 0)
            {
                FaceOverrideData[] overrides = chunk.FaceOverrides;
                remappedOverrides = new FaceOverrideData[overrides.Length];
                for (var i = 0; i < overrides.Length; i++)
                {
                    FaceOverrideData entry = overrides[i];
                    ushort texId = entry.TexturePaintId != 0 ? remapPaint(entry.TexturePaintId) : (ushort)0;
                    ushort colId = entry.ColorPaintId != 0 ? remapPaint(entry.ColorPaintId) : (ushort)0;
                    remappedOverrides[i] = new FaceOverrideData(entry.Key, texId, colId);
                }
            }

            result[c] = new ChunkInfo(
                chunkInfo.OffsetX,
                chunkInfo.OffsetY,
                chunkInfo.OffsetZ,
                new Chunk(chunk.Size, remappedVoxels, remappedOverrides)
            );
        }

        return result;
    }

    private static string[]? BuildBrickPalette(ChunkInfo[]? source, Func<ushort, string?> nameById)
    {
        if (source == null || source.Length == 0)
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

    private static string[]? BuildPaintPalette(ChunkInfo[]? source, Func<ushort, string?> nameById)
    {
        if (source == null || source.Length == 0)
        {
            return null;
        }

        ushort maxId = 0;
        for (var c = 0; c < source.Length; c++)
        {
            FaceOverrideData[]? overrides = source[c].Chunk.FaceOverrides;
            if (overrides == null)
            {
                continue;
            }

            for (var i = 0; i < overrides.Length; i++)
            {
                ushort texId = overrides[i].TexturePaintId;
                if (texId > maxId)
                {
                    maxId = texId;
                }

                ushort colId = overrides[i].ColorPaintId;
                if (colId > maxId)
                {
                    maxId = colId;
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
            FaceOverrideData[]? overrides = source[c].Chunk.FaceOverrides;
            if (overrides == null)
            {
                continue;
            }

            for (var i = 0; i < overrides.Length; i++)
            {
                RecordPaint(overrides[i].TexturePaintId, palette, nameById);
                RecordPaint(overrides[i].ColorPaintId, palette, nameById);
            }
        }

        return palette;
    }

    private static void RecordPaint(ushort id, string[] palette, Func<ushort, string?> nameById)
    {
        if (id == 0 || !string.IsNullOrEmpty(palette[id]))
        {
            return;
        }

        palette[id] = nameById(id) ?? "";
    }
}
