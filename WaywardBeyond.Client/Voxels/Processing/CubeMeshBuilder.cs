using System;
using System.Numerics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Voxels;
using WaywardBeyond.Data;
using FaceVertices = (System.Numerics.Vector3 V0, System.Numerics.Vector3 V1, System.Numerics.Vector3 V2, System.Numerics.Vector3 V3);
using FaceUVs = (System.Numerics.Vector2 U0, System.Numerics.Vector2 U1, System.Numerics.Vector2 U2, System.Numerics.Vector2 U3);

namespace WaywardBeyond.Client.Voxels.Processing;

using FaceInfo = (BrickFace Face, Vector3 Normal, FaceVertices Vertices, FaceUVs UV);

internal readonly struct CubeMeshBuilder(
    MeshState.MeshData meshData,
    VoxelSurfaceCache surfaceCache
) {
    private const int NEIGHBOR_LEFT = 0;
    private const int NEIGHBOR_RIGHT = 1;
    private const int NEIGHBOR_AHEAD = 2;
    private const int NEIGHBOR_BEHIND = 3;
    private const int NEIGHBOR_ABOVE = 4;
    private const int NEIGHBOR_BELOW = 5;

    private static readonly FaceInfo _topFace = (
        BrickFace.Top,
        new Vector3(0, 1, 0),
        (
            new Vector3(-0.5f, 0.5f,  0.5f),
            new Vector3( 0.5f, 0.5f,  0.5f),
            new Vector3( 0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f)
        ),
        (
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        )
    );
    
    private static readonly FaceInfo _bottomFace = (
        BrickFace.Bottom,
        new Vector3(0, -1, 0),
        (
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f, -0.5f,  0.5f),
            new Vector3(-0.5f, -0.5f,  0.5f)
        ),
        (
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        )
    );
    
    private static readonly FaceInfo _frontFace = (
        BrickFace.Front,
        new Vector3(0, 0, 1),
        (
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3( 0.5f, -0.5f, 0.5f),
            new Vector3( 0.5f,  0.5f, 0.5f),
            new Vector3(-0.5f,  0.5f, 0.5f)
        ),
        (
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        )
    );
    
    private static readonly FaceInfo _backFace = (
        BrickFace.Back,
        new Vector3(0, 0, -1),
        (
            new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f,  0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f, -0.5f)
        ),
        (
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        )
    );
    
    private static readonly FaceInfo _rightFace = (
        BrickFace.Right,
        new Vector3(1, 0, 0),
        (
            new Vector3(0.5f, -0.5f,  0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f,  0.5f, -0.5f),
            new Vector3(0.5f,  0.5f,  0.5f)
        ),
        (
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        )
    );
    
    private static readonly FaceInfo _leftFace = (
        BrickFace.Left,
        new Vector3(-1, 0, 0),
        (
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f, -0.5f)
        ),
        (
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        )
    );

    private static readonly RotatedFace[] _rotatedFaces = PrecomputeRotatedFaces();

    public void AddTopFace(Vector3 origin, byte orientationByte, in VoxelSample sample, in VoxelSurfaceCache.CachedBrick cachedBrick)
    {
        AddFace(in sample, neighbor: ref sample.Above, BrickFace.Top, origin, orientationByte, in cachedBrick);
    }
    
    public void AddBottomFace(Vector3 origin, byte orientationByte, in VoxelSample sample, in VoxelSurfaceCache.CachedBrick cachedBrick)
    {
        AddFace(in sample, neighbor: ref sample.Below, BrickFace.Bottom, origin, orientationByte, in cachedBrick);
    }
    
    public void AddFrontFace(Vector3 origin, byte orientationByte, in VoxelSample sample, in VoxelSurfaceCache.CachedBrick cachedBrick)
    {
        AddFace(in sample, neighbor: ref sample.Ahead, BrickFace.Front, origin, orientationByte, in cachedBrick);
    }
    
    public void AddBackFace(Vector3 origin, byte orientationByte, in VoxelSample sample, in VoxelSurfaceCache.CachedBrick cachedBrick)
    {
        AddFace(in sample, neighbor: ref sample.Behind, BrickFace.Back, origin, orientationByte, in cachedBrick);
    }
    
    public void AddRightFace(Vector3 origin, byte orientationByte, in VoxelSample sample, in VoxelSurfaceCache.CachedBrick cachedBrick)
    {
        AddFace(in sample, neighbor: ref sample.Right, BrickFace.Right, origin, orientationByte, in cachedBrick);
    }
    
    public void AddLeftFace(Vector3 origin, byte orientationByte, in VoxelSample sample, in VoxelSurfaceCache.CachedBrick cachedBrick)
    {
        AddFace(in sample, neighbor: ref sample.Left, BrickFace.Left, origin, orientationByte, in cachedBrick);
    }
    
    private void AddFace(
        in VoxelSample sample,
        ref Voxel neighbor,
        BrickFace face,
        Vector3 origin,
        byte orientationByte,
        in VoxelSurfaceCache.CachedBrick cachedBrick
    ) {
        var vertexStart = (uint)meshData.Vertices.Count;

        ResolveCenterFaceSurface(
            in sample,
            origin,
            in cachedBrick,
            face,
            out int rawTextureIndex,
            out string? textureName,
            out bool isConnected,
            out bool isSingleVariant,
            out bool hasTextureOverride,
            out Vector3 tint
        );

        int textureIndex = GetTextureIndex(
            in sample,
            orientationByte,
            rawTextureIndex,
            textureName,
            isConnected,
            isSingleVariant,
            hasTextureOverride,
            face
        );

        RotatedFace rotated = _rotatedFaces[(orientationByte * 6) + (int)face];
        
        meshData.UV.Add(new Vector3(rotated.UV.U0.X, rotated.UV.U0.Y, textureIndex));
        meshData.UV.Add(new Vector3(rotated.UV.U1.X, rotated.UV.U1.Y, textureIndex));
        meshData.UV.Add(new Vector3(rotated.UV.U2.X, rotated.UV.U2.Y, textureIndex));
        meshData.UV.Add(new Vector3(rotated.UV.U3.X, rotated.UV.U3.Y, textureIndex));
        
        meshData.Triangles.Add(vertexStart + 0);
        meshData.Triangles.Add(vertexStart + 1);
        meshData.Triangles.Add(vertexStart + 2);
        meshData.Triangles.Add(vertexStart + 0);
        meshData.Triangles.Add(vertexStart + 2);
        meshData.Triangles.Add(vertexStart + 3);

        meshData.Normals.Add(rotated.Normal);
        meshData.Normals.Add(rotated.Normal);
        meshData.Normals.Add(rotated.Normal);
        meshData.Normals.Add(rotated.Normal);
        
        //  VertexColor.rgb carries linear albedo tint; VertexColor.a carries voxel light.
        ShapeLight shapeLight = neighbor.ShapeLight;
        float light = Math.Clamp(shapeLight.LightLevel / 15f, 0.1f, 1f);
        var color = new Vector4(tint.X, tint.Y, tint.Z, light);
        meshData.Colors.Add(color);
        meshData.Colors.Add(color);
        meshData.Colors.Add(color);
        meshData.Colors.Add(color);
        
        meshData.Vertices.Add(origin + rotated.V0);
        meshData.Vertices.Add(origin + rotated.V1);
        meshData.Vertices.Add(origin + rotated.V2);
        meshData.Vertices.Add(origin + rotated.V3);
    }

    private void ResolveCenterFaceSurface(
        in VoxelSample sample,
        Vector3 origin,
        in VoxelSurfaceCache.CachedBrick cachedBrick,
        BrickFace face,
        out int textureIndex,
        out string? textureName,
        out bool isConnected,
        out bool isSingleVariant,
        out bool hasTextureOverride,
        out Vector3 tint
    ) {
        tint = Vector3.One;
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
                }
            }

            if (faceOverride.TexturePaintId != 0)
            {
                hasTextureOverride = true;
                ref readonly VoxelSurfaceCache.CachedPaint texturePaint = ref surfaceCache.GetPaint(faceOverride.TexturePaintId);
                if (texturePaint.Exists && texturePaint.Kind == PaintKind.Texture)
                {
                    texturePaint.Textures.Resolve(origin, out textureIndex, out textureName);
                    isConnected = texturePaint.Connected;
                    isSingleVariant = texturePaint.Textures.IsSingleVariant;
                    return;
                }

                //  Unresolved extension paint id falls back to layer 0 (missing checkerboard), non-connected.
                textureIndex = -1;
                textureName = null;
                isConnected = false;
                isSingleVariant = false;
                return;
            }
        }

        hasTextureOverride = false;
        ref readonly VoxelSurfaceCache.ResolvedTextureList textures = ref VoxelSurfaceCache.CachedBrick.GetFaceTextures(in cachedBrick, face);
        textures.Resolve(origin, out textureIndex, out textureName);
        isConnected = cachedBrick.Connected;
        isSingleVariant = textures.IsSingleVariant;
    }

    /// <summary>
    ///     Gets the best matching texture array index for a face, applying 4-bit connected-texture
    ///     adjacency across both native bricks and painted faces when enabled.
    /// </summary>
    private int GetTextureIndex(
        in VoxelSample sample,
        byte centerOrientationByte,
        int rawTextureIndex,
        string? textureName,
        bool isConnected,
        bool isSingleVariant,
        bool hasTextureOverride,
        BrickFace face
    ) {
        int textureIndex = rawTextureIndex >= 0 ? rawTextureIndex : 0;
        
        if (!isConnected || textureName == null)
        {
            return textureIndex;
        }
        
        ShapeLight shapeLight = sample.Center.ShapeLight;
        var centerOrientation = new Orientation(centerOrientationByte);
        BrickFace worldFace = FaceOrientationLookup.ToWorldFace(centerOrientation, face);
        var connectedTextureMask = 0;
        
        //  Neighbor bit masking is ordered: Up, left, right, down
        switch (face)
        {
            case BrickFace.Top:
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Behind, NEIGHBOR_BEHIND, bit: 1, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Left, NEIGHBOR_LEFT, bit: 2, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Right, NEIGHBOR_RIGHT, bit: 4, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Ahead, NEIGHBOR_AHEAD, bit: 8, ref connectedTextureMask);
                break;
            case BrickFace.Bottom:
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Ahead, NEIGHBOR_AHEAD, bit: 1, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Left, NEIGHBOR_LEFT, bit: 2, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Right, NEIGHBOR_RIGHT, bit: 4, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Behind, NEIGHBOR_BEHIND, bit: 8, ref connectedTextureMask);
                break;
            case BrickFace.Left:
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Above, NEIGHBOR_ABOVE, bit: 1, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Behind, NEIGHBOR_BEHIND, bit: 2, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Ahead, NEIGHBOR_AHEAD, bit: 4, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Below, NEIGHBOR_BELOW, bit: 8, ref connectedTextureMask);
                break;
            case BrickFace.Right:
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Above, NEIGHBOR_ABOVE, bit: 1, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Ahead, NEIGHBOR_AHEAD, bit: 2, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Behind, NEIGHBOR_BEHIND, bit: 4, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Below, NEIGHBOR_BELOW, bit: 8, ref connectedTextureMask);
                break;
            case BrickFace.Front:
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Above, NEIGHBOR_ABOVE, bit: 1, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Left, NEIGHBOR_LEFT, bit: 2, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Right, NEIGHBOR_RIGHT, bit: 4, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Below, NEIGHBOR_BELOW, bit: 8, ref connectedTextureMask);
                break;
            case BrickFace.Back:
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Above, NEIGHBOR_ABOVE, bit: 1, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Right, NEIGHBOR_RIGHT, bit: 2, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Left, NEIGHBOR_LEFT, bit: 4, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, worldFace, rawTextureIndex, textureName, isSingleVariant, hasTextureOverride, shapeLight, in sample.Below, NEIGHBOR_BELOW, bit: 8, ref connectedTextureMask);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(face), face, null);
        }
        
        return textureIndex + connectedTextureMask;
    }
    
    private void ConnectToNeighbor(
        in VoxelSample sample,
        Orientation centerOrientation,
        BrickFace centerFace,
        BrickFace worldFace,
        int centerTextureIndex,
        string centerTextureName,
        bool centerIsSingleVariant,
        bool centerHasTextureOverride,
        ShapeLight centerShapeLight,
        in Voxel neighbor,
        int neighborDirIndex,
        int bit,
        ref int mask
    ) {
        if (neighbor.ID == 0)
        {
            return;
        }

        ShapeLight neighborShapeLight = neighbor.ShapeLight;
        if (neighborShapeLight.Shape != centerShapeLight.Shape)
        {
            return;
        }

        Orientation neighborOrientation = neighbor.GetOrientation();
        BrickFace neighborFace = neighborOrientation == centerOrientation
            ? centerFace
            : FaceOrientationLookup.ToLocalFace(neighborOrientation, worldFace);

        bool neighborHasOverrides = neighbor.HasFaceOverrides();
        if (!centerHasTextureOverride &&
            !neighborHasOverrides &&
            neighbor.ID == sample.Center.ID &&
            neighborFace == centerFace &&
            centerIsSingleVariant)
        {
            mask |= bit;
            return;
        }

        Int3 offset = ChunkData.GetRotatedNeighborOffset(centerOrientation, neighborDirIndex);
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
            (neighborTextureIndex >= 0 && centerTextureIndex >= 0
                ? neighborTextureIndex == centerTextureIndex
                : string.Equals(neighborTextureName, centerTextureName, StringComparison.Ordinal)))
        {
            mask |= bit;
        }
    }

    private static RotatedFace[] PrecomputeRotatedFaces()
    {
        FaceInfo[] baseFaces = [_topFace, _bottomFace, _frontFace, _backFace, _leftFace, _rightFace];
        var table = new RotatedFace[64 * 6];
        for (byte o = 0; o < 64; o++)
        {
            Quaternion q = new Orientation(o).ToQuaternion();
            int baseIndex = o * 6;
            for (var f = 0; f < 6; f++)
            {
                FaceInfo face = baseFaces[f];
                table[baseIndex + (int)face.Face] = new RotatedFace(
                    Vector3.Transform(face.Normal, q),
                    Vector3.Transform(face.Vertices.V0, q),
                    Vector3.Transform(face.Vertices.V1, q),
                    Vector3.Transform(face.Vertices.V2, q),
                    Vector3.Transform(face.Vertices.V3, q),
                    face.UV
                );
            }
        }

        return table;
    }
    
    private readonly struct RotatedFace(Vector3 normal, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, FaceUVs uv)
    {
        public readonly Vector3 Normal = normal;
        public readonly Vector3 V0 = v0;
        public readonly Vector3 V1 = v1;
        public readonly Vector3 V2 = v2;
        public readonly Vector3 V3 = v3;
        public readonly FaceUVs UV = uv;
    }
}
