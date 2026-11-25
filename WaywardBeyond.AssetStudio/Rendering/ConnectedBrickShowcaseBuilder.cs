using System.Collections.Generic;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Constructs a compact 5x5x1 connected-texture showcase voxel region exhibiting all 16 cardinal adjacency masks (0..15).</summary>
public static class ConnectedBrickShowcaseBuilder
{
    /// <summary>Width in voxels of the connected-texture showcase arrangement.</summary>
    public const int SHOWCASE_WIDTH = 5;

    /// <summary>Height in voxels of the connected-texture showcase arrangement.</summary>
    public const int SHOWCASE_HEIGHT = 5;

    /// <summary>Depth in voxels of the connected-texture showcase arrangement.</summary>
    public const int SHOWCASE_DEPTH = 1;

    private const byte SHOWCASE_CHUNK_SIZE = 8;
    private const int PREVIEW_LIGHT_LEVEL = 15;

    /// <summary>
    ///     Ordered 16 voxel coordinates in the 5x5x1 arrangement (3x3 panel, 1x3 vertical pillar, 3x1 horizontal bar, 1x1 isolated block).
    ///     Each voxel's front (+Z) face exhibits a distinct 4-bit cardinal mask (Up=1, Left=2, Right=4, Down=8) covering 0..15 bijectively.
    /// </summary>
    public static IReadOnlyList<(Int3 Coords, byte ExpectedFrontMask)> ShowcaseLayout { get; } =
    [
        // 3x3 solid panel (x = 0..2, y = 2..4): corners, edges, and center
        (new Int3(0, 4, 0), 12), // Right + Down (top-left corner)
        (new Int3(1, 4, 0), 14), // Left + Right + Down (top edge)
        (new Int3(2, 4, 0), 10), // Left + Down (top-right corner)
        (new Int3(0, 3, 0), 13), // Up + Right + Down (left edge)
        (new Int3(1, 3, 0), 15), // Up + Left + Right + Down (fully connected center)
        (new Int3(2, 3, 0), 11), // Up + Left + Down (right edge)
        (new Int3(0, 2, 0), 5),  // Up + Right (bottom-left corner)
        (new Int3(1, 2, 0), 7),  // Up + Left + Right (bottom edge)
        (new Int3(2, 2, 0), 3),  // Up + Left (bottom-right corner)

        // 3x1 horizontal bar (x = 0..2, y = 0): left cap, bridge, right cap
        (new Int3(0, 0, 0), 4),  // Right (left end-cap)
        (new Int3(1, 0, 0), 6),  // Left + Right (horizontal bridge)
        (new Int3(2, 0, 0), 2),  // Left (right end-cap)

        // 1x3 vertical pillar (x = 4, y = 2..4): top cap, bridge, bottom cap
        (new Int3(4, 4, 0), 8),  // Down (top end-cap)
        (new Int3(4, 3, 0), 9),  // Up + Down (vertical bridge)
        (new Int3(4, 2, 0), 1),  // Up (bottom end-cap)

        // 1x1 isolated single block (x = 4, y = 0): all 4 edges unconnected
        (new Int3(4, 0, 0), 0),  // None (isolated block)
    ];

    /// <summary>Builds a 5x5x1 <see cref="VoxelRegionData"/> populated with <paramref name="brickDataId"/> at <paramref name="orientation"/>.</summary>
    public static Result<VoxelRegionData> BuildRegion(ushort brickDataId, Orientation orientation)
    {
        if (brickDataId == 0)
        {
            return Result<VoxelRegionData>.FromFailure("Connected showcase requires a non-zero registered brick voxel DataID.");
        }

        var voxelObject = new VoxelObject(SHOWCASE_CHUNK_SIZE);
        var voxel = new Voxel(
            brickDataId,
            new ShapeLight(BrickShape.Block, PREVIEW_LIGHT_LEVEL),
            new OrientationFlags(orientation, hasFaceOverrides: false));

        for (var i = 0; i < ShowcaseLayout.Count; i++)
        {
            Int3 coords = ShowcaseLayout[i].Coords;
            voxelObject.Set(coords.X, coords.Y, coords.Z, voxel);
        }

        if (!voxelObject.TryGetChunk(new Short3(0, 0, 0), out ChunkData chunk))
        {
            return Result<VoxelRegionData>.FromFailure("Failed to allocate chunk for connected brick showcase.");
        }

        var region = new VoxelRegionData(
            SHOWCASE_WIDTH,
            SHOWCASE_HEIGHT,
            SHOWCASE_DEPTH,
            [chunk.ToChunkInfo()],
            null,
            null);
        return Result<VoxelRegionData>.FromSuccess(region);
    }

    /// <summary>Resolves <paramref name="brick"/> in <paramref name="brickRegistry"/> and builds its 5x5x1 connected showcase region.</summary>
    public static Result<VoxelRegionData> BuildRegion(Brick brick, IBrickRegistry brickRegistry, Orientation orientation)
    {
        if (brick == null)
        {
            return Result<VoxelRegionData>.FromFailure("Brick is null.");
        }

        ushort resolvedId = brickRegistry != null ? brickRegistry.Id(brick.ID) : brick.DataID;
        if (resolvedId == 0)
        {
            resolvedId = brick.DataID;
        }

        return BuildRegion(resolvedId, orientation);
    }
}
