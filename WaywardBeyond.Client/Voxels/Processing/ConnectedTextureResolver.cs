using System;
using System.Numerics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Processing;

/// <summary>Resolves per-face surface textures, tints, and 4-bit connected-texture adjacency for voxel meshing.</summary>
internal readonly struct ConnectedTextureResolver(VoxelSurfaceCache surfaceCache)
{
    private const int NEIGHBOR_LEFT = 0;
    private const int NEIGHBOR_RIGHT = 1;
    private const int NEIGHBOR_AHEAD = 2;
    private const int NEIGHBOR_BEHIND = 3;
    private const int NEIGHBOR_ABOVE = 4;
    private const int NEIGHBOR_BELOW = 5;

    private const byte EDGE_NONE = 0;
    private const byte EDGE_UP = 1;
    private const byte EDGE_LEFT = 2;
    private const byte EDGE_RIGHT = 4;
    private const byte EDGE_DOWN = 8;
    private const byte EDGE_HORIZONTAL = EDGE_LEFT | EDGE_RIGHT;
    private const byte EDGE_VERTICAL = EDGE_UP | EDGE_DOWN;
    private const byte EDGE_ALL = EDGE_UP | EDGE_LEFT | EDGE_RIGHT | EDGE_DOWN;

    private const int FACE_COUNT = 6;

    //  Indexed by ((int)BrickShape * 6) + (int)BrickFace in (Top, Bottom, Front, Back, Left, Right) order.
    private static readonly byte[] _shapeFaceEdgeMasks =
    [
        //  Block
        EDGE_ALL, EDGE_ALL, EDGE_ALL, EDGE_ALL, EDGE_ALL, EDGE_ALL,
        //  Slab
        EDGE_ALL, EDGE_ALL, EDGE_HORIZONTAL, EDGE_HORIZONTAL, EDGE_HORIZONTAL, EDGE_HORIZONTAL,
        //  Stair
        EDGE_HORIZONTAL, EDGE_ALL, EDGE_HORIZONTAL, EDGE_ALL, EDGE_NONE, EDGE_NONE,
        //  Slope
        EDGE_NONE, EDGE_ALL, EDGE_ALL, EDGE_ALL, EDGE_NONE, EDGE_NONE,
        //  Column
        EDGE_NONE, EDGE_NONE, EDGE_VERTICAL, EDGE_VERTICAL, EDGE_VERTICAL, EDGE_VERTICAL,
        //  Plate
        EDGE_ALL, EDGE_ALL, EDGE_HORIZONTAL, EDGE_HORIZONTAL, EDGE_HORIZONTAL, EDGE_HORIZONTAL,
        //  Custom
        EDGE_NONE, EDGE_NONE, EDGE_NONE, EDGE_NONE, EDGE_NONE, EDGE_NONE,
        //  Any
        EDGE_NONE, EDGE_NONE, EDGE_NONE, EDGE_NONE, EDGE_NONE, EDGE_NONE,
    ];

    /// <summary>Returns true when <paramref name="shape"/> participates in connected-texture adjacency.</summary>
    public static bool SupportsConnectedTextures(BrickShape shape)
    {
        return shape is BrickShape.Block
            or BrickShape.Slab
            or BrickShape.Stair
            or BrickShape.Slope
            or BrickShape.Column
            or BrickShape.Plate;
    }

    /// <summary>Returns the 4-bit edge mask of neighbor directions that physically meet on <paramref name="face"/> for <paramref name="shape"/>.</summary>
    public static byte GetAllowedEdgeMask(BrickShape shape, BrickFace face)
    {
        int index = ((int)shape * FACE_COUNT) + (int)face;
        return (uint)index < (uint)_shapeFaceEdgeMasks.Length
            ? _shapeFaceEdgeMasks[index]
            : EDGE_NONE;
    }

    /// <summary>Resolves the base texture, connected-texture flag, and color tint for <paramref name="face"/>.</summary>
    public FaceSurface ResolveFaceSurface(
        in VoxelSample sample,
        Vector3 origin,
        in VoxelSurfaceCache.CachedBrick cachedBrick,
        BrickFace face
    ) {
        Vector3 tint = Vector3.One;
        var hasTint = false;

        if (sample.Center.HasFaceOverrides() &&
            sample.VoxelIndex >= 0 &&
            sample.ChunkData.TryGetFaceOverride(sample.VoxelIndex, face, out FaceOverride faceOverride))
        {
            if (faceOverride.ColorPaintId != 0)
            {
                ref readonly VoxelSurfaceCache.CachedPaint colorPaint = ref surfaceCache.GetPaint(faceOverride.ColorPaintId);
                if (colorPaint.Exists && colorPaint.Kind == PaintKind.Color)
                {
                    tint = colorPaint.LinearColor;
                    hasTint = true;
                }
            }

            if (faceOverride.TexturePaintId != 0)
            {
                ref readonly VoxelSurfaceCache.CachedPaint texturePaint = ref surfaceCache.GetPaint(faceOverride.TexturePaintId);
                if (texturePaint.Exists && texturePaint.Kind == PaintKind.Texture)
                {
                    texturePaint.Textures.Resolve(origin, out int paintTextureIndex, out string? paintTextureName);
                    return new FaceSurface(
                        rawTextureIndex: paintTextureIndex,
                        textureName: paintTextureName,
                        isConnected: texturePaint.Connected,
                        isSingleVariant: texturePaint.Textures.IsSingleVariant,
                        hasTextureOverride: true,
                        hasDeclaredFaceTexture: false,
                        tint: tint,
                        hasTint: hasTint
                    );
                }

                //  Unresolved extension paint id falls back to layer 0 (missing checkerboard), non-connected.
                return new FaceSurface(
                    rawTextureIndex: -1,
                    textureName: null,
                    isConnected: false,
                    isSingleVariant: false,
                    hasTextureOverride: true,
                    hasDeclaredFaceTexture: false,
                    tint: tint,
                    hasTint: hasTint
                );
            }
        }

        bool isDeclaredFace = VoxelSurfaceCache.CachedBrick.HasDeclaredFaceTextures(in cachedBrick, face);
        ref readonly VoxelSurfaceCache.ResolvedTextureList textures = ref VoxelSurfaceCache.CachedBrick.GetFaceTextures(in cachedBrick, face);
        textures.Resolve(origin, out int textureIndex, out string? textureName);
        return new FaceSurface(
            rawTextureIndex: textureIndex,
            textureName: textureName,
            isConnected: cachedBrick.Connected,
            isSingleVariant: textures.IsSingleVariant,
            hasTextureOverride: false,
            hasDeclaredFaceTexture: isDeclaredFace && textureName != null,
            tint: tint,
            hasTint: hasTint
        );
    }

    /// <summary>Gets the texture array index for <paramref name="face"/>, applying 4-bit connected-texture adjacency when enabled.</summary>
    public int ResolveTextureIndex(
        in VoxelSample sample,
        byte centerOrientationByte,
        in FaceSurface surface,
        BrickFace face
    ) {
        int textureIndex = surface.RawTextureIndex >= 0 ? surface.RawTextureIndex : 0;
        if (!surface.IsConnected || surface.TextureName == null)
        {
            return textureIndex;
        }

        ShapeLight shapeLight = sample.Center.ShapeLight;
        byte allowedEdgeMask = GetAllowedEdgeMask(shapeLight.Shape, face);
        if (allowedEdgeMask == EDGE_NONE)
        {
            return textureIndex;
        }

        var centerOrientation = new Orientation(centerOrientationByte);
        BrickFace worldFace = FaceOrientationLookup.ToWorldFace(centerOrientation, face);
        var context = new ConnectionContext(centerOrientation, face, worldFace, shapeLight, surface);
        var connectedTextureMask = 0;

        //  Neighbor bit masking is ordered: Up (1), Left (2), Right (4), Down (8).
        switch (face)
        {
            case BrickFace.Top:
                ConnectEdges(in sample, in context, allowedEdgeMask, in sample.Behind, NEIGHBOR_BEHIND, in sample.Left, NEIGHBOR_LEFT, in sample.Right, NEIGHBOR_RIGHT, in sample.Ahead, NEIGHBOR_AHEAD, ref connectedTextureMask);
                break;
            case BrickFace.Bottom:
                ConnectEdges(in sample, in context, allowedEdgeMask, in sample.Ahead, NEIGHBOR_AHEAD, in sample.Left, NEIGHBOR_LEFT, in sample.Right, NEIGHBOR_RIGHT, in sample.Behind, NEIGHBOR_BEHIND, ref connectedTextureMask);
                break;
            case BrickFace.Left:
                ConnectEdges(in sample, in context, allowedEdgeMask, in sample.Above, NEIGHBOR_ABOVE, in sample.Behind, NEIGHBOR_BEHIND, in sample.Ahead, NEIGHBOR_AHEAD, in sample.Below, NEIGHBOR_BELOW, ref connectedTextureMask);
                break;
            case BrickFace.Right:
                ConnectEdges(in sample, in context, allowedEdgeMask, in sample.Above, NEIGHBOR_ABOVE, in sample.Ahead, NEIGHBOR_AHEAD, in sample.Behind, NEIGHBOR_BEHIND, in sample.Below, NEIGHBOR_BELOW, ref connectedTextureMask);
                break;
            case BrickFace.Front:
                if (shapeLight.Shape == BrickShape.Slope)
                {
                    ConnectSlopeDiagonalEdges(in sample, in context, ref connectedTextureMask);
                }
                else
                {
                    ConnectEdges(in sample, in context, allowedEdgeMask, in sample.Above, NEIGHBOR_ABOVE, in sample.Left, NEIGHBOR_LEFT, in sample.Right, NEIGHBOR_RIGHT, in sample.Below, NEIGHBOR_BELOW, ref connectedTextureMask);
                }
                break;
            case BrickFace.Back:
                ConnectEdges(in sample, in context, allowedEdgeMask, in sample.Above, NEIGHBOR_ABOVE, in sample.Right, NEIGHBOR_RIGHT, in sample.Left, NEIGHBOR_LEFT, in sample.Below, NEIGHBOR_BELOW, ref connectedTextureMask);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(face), face, null);
        }

        return textureIndex + connectedTextureMask;
    }

    private void ConnectEdges(
        in VoxelSample sample,
        in ConnectionContext context,
        byte allowedEdgeMask,
        in Voxel upNeighbor,
        int upDirIndex,
        in Voxel leftNeighbor,
        int leftDirIndex,
        in Voxel rightNeighbor,
        int rightDirIndex,
        in Voxel downNeighbor,
        int downDirIndex,
        ref int mask
    ) {
        if ((allowedEdgeMask & EDGE_UP) != 0)
        {
            ConnectToNeighbor(in sample, in context, in upNeighbor, upDirIndex, EDGE_UP, ref mask);
        }

        if ((allowedEdgeMask & EDGE_LEFT) != 0)
        {
            ConnectToNeighbor(in sample, in context, in leftNeighbor, leftDirIndex, EDGE_LEFT, ref mask);
        }

        if ((allowedEdgeMask & EDGE_RIGHT) != 0)
        {
            ConnectToNeighbor(in sample, in context, in rightNeighbor, rightDirIndex, EDGE_RIGHT, ref mask);
        }

        if ((allowedEdgeMask & EDGE_DOWN) != 0)
        {
            ConnectToNeighbor(in sample, in context, in downNeighbor, downDirIndex, EDGE_DOWN, ref mask);
        }
    }

    private void ConnectSlopeDiagonalEdges(
        in VoxelSample sample,
        in ConnectionContext context,
        ref int mask
    ) {
        Int3 aboveOffset = ChunkData.GetRotatedNeighborOffset(context.CenterOrientation, NEIGHBOR_ABOVE);
        Int3 behindOffset = ChunkData.GetRotatedNeighborOffset(context.CenterOrientation, NEIGHBOR_BEHIND);
        var upRampOffset = new Int3(aboveOffset.X + behindOffset.X, aboveOffset.Y + behindOffset.Y, aboveOffset.Z + behindOffset.Z);
        if (TryGetNeighborAtOffset(in sample, upRampOffset, out Voxel upRampNeighbor))
        {
            ConnectToNeighborAtOffset(in sample, in context, in upRampNeighbor, upRampOffset, EDGE_UP, ref mask);
        }

        ConnectToNeighbor(in sample, in context, in sample.Left, NEIGHBOR_LEFT, EDGE_LEFT, ref mask);
        ConnectToNeighbor(in sample, in context, in sample.Right, NEIGHBOR_RIGHT, EDGE_RIGHT, ref mask);

        Int3 belowOffset = ChunkData.GetRotatedNeighborOffset(context.CenterOrientation, NEIGHBOR_BELOW);
        Int3 aheadOffset = ChunkData.GetRotatedNeighborOffset(context.CenterOrientation, NEIGHBOR_AHEAD);
        var downRampOffset = new Int3(belowOffset.X + aheadOffset.X, belowOffset.Y + aheadOffset.Y, belowOffset.Z + aheadOffset.Z);
        if (TryGetNeighborAtOffset(in sample, downRampOffset, out Voxel downRampNeighbor))
        {
            ConnectToNeighborAtOffset(in sample, in context, in downRampNeighbor, downRampOffset, EDGE_DOWN, ref mask);
        }
    }

    private static bool TryGetNeighborAtOffset(in VoxelSample sample, Int3 offset, out Voxel neighbor)
    {
        int localNx = sample.Coords.X + offset.X;
        int localNy = sample.Coords.Y + offset.Y;
        int localNz = sample.Coords.Z + offset.Z;

        Voxel[]? voxels = sample.ChunkData.Data.Voxels;
        if (voxels != null)
        {
            int chunkSize = sample.ChunkData.Data.Size;
            if ((uint)localNx < (uint)chunkSize && (uint)localNy < (uint)chunkSize && (uint)localNz < (uint)chunkSize)
            {
                int shift = BitOperations.TrailingZeroCount(chunkSize);
                int neighborIndex = localNx + (localNy << shift) + (localNz << (shift * 2));
                neighbor = voxels[neighborIndex];
                return true;
            }
        }

        if (sample.VoxelObject != null)
        {
            neighbor = sample.VoxelObject.Get(sample.ChunkOffset.X + localNx, sample.ChunkOffset.Y + localNy, sample.ChunkOffset.Z + localNz);
            return true;
        }

        neighbor = default;
        return false;
    }

    private static bool AreOrientationsCompatible(BrickShape shape, Orientation centerOrientation, Orientation neighborOrientation)
    {
        if (centerOrientation == neighborOrientation)
        {
            return true;
        }

        switch (shape)
        {
            case BrickShape.Block:
                return true;
            case BrickShape.Slab:
            case BrickShape.Plate:
                return FaceOrientationLookup.ToWorldFace(centerOrientation, BrickFace.Top) ==
                    FaceOrientationLookup.ToWorldFace(neighborOrientation, BrickFace.Top);
            case BrickShape.Column:
                BrickFace centerTop = FaceOrientationLookup.ToWorldFace(centerOrientation, BrickFace.Top);
                BrickFace neighborTop = FaceOrientationLookup.ToWorldFace(neighborOrientation, BrickFace.Top);
                return ((int)centerTop >> 1) == ((int)neighborTop >> 1);
            default:
                return false;
        }
    }

    private void ConnectToNeighbor(
        in VoxelSample sample,
        in ConnectionContext context,
        in Voxel neighbor,
        int neighborDirIndex,
        int bit,
        ref int mask
    ) {
        Int3 offset = ChunkData.GetRotatedNeighborOffset(context.CenterOrientation, neighborDirIndex);
        ConnectToNeighborAtOffset(in sample, in context, in neighbor, offset, bit, ref mask);
    }

    private void ConnectToNeighborAtOffset(
        in VoxelSample sample,
        in ConnectionContext context,
        in Voxel neighbor,
        Int3 offset,
        int bit,
        ref int mask
    ) {
        if (neighbor.ID == 0)
        {
            return;
        }

        ShapeLight neighborShapeLight = neighbor.ShapeLight;
        if (neighborShapeLight.Shape != context.ShapeLight.Shape)
        {
            return;
        }

        Orientation neighborOrientation = neighbor.GetOrientation();
        if (!AreOrientationsCompatible(context.ShapeLight.Shape, context.CenterOrientation, neighborOrientation))
        {
            return;
        }

        BrickFace neighborFace = neighborOrientation == context.CenterOrientation
            ? context.CenterFace
            : FaceOrientationLookup.ToLocalFace(neighborOrientation, context.WorldFace);

        bool neighborHasOverrides = neighbor.HasFaceOverrides();
        if (!context.Surface.HasTextureOverride &&
            !neighborHasOverrides &&
            neighbor.ID == sample.Center.ID &&
            neighborFace == context.CenterFace &&
            context.Surface.IsSingleVariant)
        {
            mask |= bit;
            return;
        }

        int localNx = sample.Coords.X + offset.X;
        int localNy = sample.Coords.Y + offset.Y;
        int localNz = sample.Coords.Z + offset.Z;
        int nx = sample.ChunkOffset.X + localNx;
        int ny = sample.ChunkOffset.Y + localNy;
        int nz = sample.ChunkOffset.Z + localNz;
        var neighborOrigin = new Vector3(nx, ny, nz);

        int neighborTextureIndex;
        string? neighborTextureName;
        bool neighborConnected;

        var foundOverride = false;
        FaceOverride neighborOverride = default;
        if (neighborHasOverrides)
        {
            int chunkSize = sample.ChunkData.Data.Size;
            if ((uint)localNx < (uint)chunkSize && (uint)localNy < (uint)chunkSize && (uint)localNz < (uint)chunkSize)
            {
                int shift = BitOperations.TrailingZeroCount(chunkSize);
                int neighborIndex = localNx + (localNy << shift) + (localNz << (shift * 2));
                foundOverride = sample.ChunkData.TryGetFaceOverride(neighborIndex, neighborFace, out neighborOverride);
            }
            else if (sample.VoxelObject != null)
            {
                foundOverride = sample.VoxelObject.TryGetFaceOverride(nx, ny, nz, neighborFace, out neighborOverride);
            }
        }

        if (foundOverride && neighborOverride.TexturePaintId != 0)
        {
            ref readonly VoxelSurfaceCache.CachedPaint paint = ref surfaceCache.GetPaint(neighborOverride.TexturePaintId);
            if (!paint.Exists || paint.Kind != PaintKind.Texture)
            {
                return;
            }

            paint.Textures.Resolve(neighborOrigin, out neighborTextureIndex, out neighborTextureName);
            neighborConnected = paint.Connected;
        }
        else
        {
            ref readonly VoxelSurfaceCache.CachedBrick neighborBrick = ref surfaceCache.GetBrick(neighbor.ID);
            if (!neighborBrick.Exists)
            {
                return;
            }

            VoxelSurfaceCache.CachedBrick.GetFaceTextures(in neighborBrick, neighborFace).Resolve(neighborOrigin, out neighborTextureIndex, out neighborTextureName);
            neighborConnected = neighborBrick.Connected;
        }

        if (neighborConnected &&
            (neighborTextureIndex >= 0 && context.Surface.RawTextureIndex >= 0
                ? neighborTextureIndex == context.Surface.RawTextureIndex
                : string.Equals(neighborTextureName, context.Surface.TextureName, StringComparison.Ordinal)))
        {
            mask |= bit;
        }
    }

    /// <summary>Resolved base texture and tint metadata for a single brick-local face.</summary>
    internal readonly struct FaceSurface(
        int rawTextureIndex,
        string? textureName,
        bool isConnected,
        bool isSingleVariant,
        bool hasTextureOverride,
        bool hasDeclaredFaceTexture,
        Vector3 tint,
        bool hasTint
    ) {
        public readonly int RawTextureIndex = rawTextureIndex;
        public readonly string? TextureName = textureName;
        public readonly bool IsConnected = isConnected;
        public readonly bool IsSingleVariant = isSingleVariant;
        public readonly bool HasTextureOverride = hasTextureOverride;
        public readonly bool HasDeclaredFaceTexture = hasDeclaredFaceTexture;
        public readonly Vector3 Tint = tint;
        public readonly bool HasTint = hasTint;
    }

    private readonly struct ConnectionContext(
        Orientation centerOrientation,
        BrickFace centerFace,
        BrickFace worldFace,
        ShapeLight shapeLight,
        FaceSurface surface
    ) {
        public readonly Orientation CenterOrientation = centerOrientation;
        public readonly BrickFace CenterFace = centerFace;
        public readonly BrickFace WorldFace = worldFace;
        public readonly ShapeLight ShapeLight = shapeLight;
        public readonly FaceSurface Surface = surface;
    }
}
