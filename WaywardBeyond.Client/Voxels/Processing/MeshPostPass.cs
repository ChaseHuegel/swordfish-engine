using System;
using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Voxels;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Voxels.Processing;

internal sealed class MeshPostPass : VoxelObjectProcessor.ISamplePass
{
    private const float NORMAL_EPSILON = 1e-4f;
    private const float SECONDARY_AXIS_RATIO = 0.5f;
    
    private static readonly Brick _unresolvedBrick = new(
        id: string.Empty,
        alpha: BrickAlpha.Opaque,
        passable: false,
        meshID: null,
        shape: BrickShape.Any,
        textures: default
    );

    private readonly MeshState _meshState;
    private readonly VoxelSurfaceCache _surfaceCache;
    private readonly CubeMeshBuilder _opaqueCubeMeshBuilder;
    private readonly CubeMeshBuilder _opaqueNoShadowCubeMeshBuilder;
    private readonly CubeMeshBuilder _maskedCubeMeshBuilder;
    private readonly CubeMeshBuilder _transparentCubeMeshBuilder;
    private readonly Mesh _slope;
    private readonly Mesh _stair;
    private readonly Mesh _slab;
    private readonly Mesh _column;
    private readonly Mesh _plate;

    public MeshPostPass(
        MeshState meshState,
        IBrickDatabase brickDatabase,
        PBRTextureArrays textureArrays,
        IAssetDatabase<Mesh> meshDatabase,
        IPaintDatabase? paintDatabase = null
    ) {
        _meshState = meshState;
        _surfaceCache = new VoxelSurfaceCache(brickDatabase, textureArrays.Albedo, meshDatabase, paintDatabase);
        _opaqueCubeMeshBuilder = new CubeMeshBuilder(meshState.Opaque, _surfaceCache);
        _opaqueNoShadowCubeMeshBuilder = new CubeMeshBuilder(meshState.OpaqueNoShadow, _surfaceCache);
        _maskedCubeMeshBuilder = new CubeMeshBuilder(meshState.Masked, _surfaceCache);
        _transparentCubeMeshBuilder = new CubeMeshBuilder(meshState.Transparent, _surfaceCache);
        _slope = meshDatabase.Get("slope.obj").Value;
        _stair = meshDatabase.Get("stair.obj").Value;
        _slab = meshDatabase.Get("slab.obj").Value;
        _column = meshDatabase.Get("column.obj").Value;
        _plate = meshDatabase.Get("plate.obj").Value;
    }
    
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PostPass;

    public bool ShouldProcessChunk(ChunkData chunkData)
    {
        return !chunkData.Palette.Only(id: 0);
    }

    public void Process(VoxelSample sample)
    {
        if (sample.Center.ID == 0)
        {
            return;
        }
        
        ref readonly VoxelSurfaceCache.CachedBrick cachedBrick = ref _surfaceCache.GetBrick(sample.Center.ID);
        Brick brickInfo = cachedBrick.Exists ? cachedBrick.Brick : _unresolvedBrick;
        
        bool culledRight = IsCulledBy(target: sample.Center, neighbor: sample.Right);
        bool culledLeft = IsCulledBy(target: sample.Center, neighbor: sample.Left);
        bool culledAbove = IsCulledBy(target: sample.Center, neighbor: sample.Above);
        bool culledBelow = IsCulledBy(target: sample.Center, neighbor: sample.Below);
        bool culledAhead = IsCulledBy(target: sample.Center, neighbor: sample.Ahead);
        bool culledBehind = IsCulledBy(target: sample.Center, neighbor: sample.Behind);
        
        if (culledRight && culledLeft && culledAbove && culledBelow && culledAhead && culledBehind)
        {
            return;
        }
        
        byte orientationByte = sample.Center.GetOrientation().ToByte();
        ShapeLight shapeLight = sample.Center.ShapeLight;
        var offset = new Vector3(sample.ChunkOffset.X, sample.ChunkOffset.Y, sample.ChunkOffset.Z);

        switch (shapeLight.Shape)
        {
            case BrickShape.Slab:
                AddMesh(GetMeshData(brickInfo), in sample, offset, in cachedBrick, _slab, new Orientation(orientationByte).ToQuaternion(), shapeLight.LightLevel);
                break;
            case BrickShape.Stair:
                AddMesh(GetMeshData(brickInfo), in sample, offset, in cachedBrick, _stair, new Orientation(orientationByte).ToQuaternion(), shapeLight.LightLevel);
                break;
            case BrickShape.Slope:
                AddMesh(GetMeshData(brickInfo), in sample, offset, in cachedBrick, _slope, new Orientation(orientationByte).ToQuaternion(), shapeLight.LightLevel);
                break;
            case BrickShape.Column:
                AddMesh(GetMeshData(brickInfo), in sample, offset, in cachedBrick, _column, new Orientation(orientationByte).ToQuaternion(), shapeLight.LightLevel);
                break;
            case BrickShape.Plate:
                AddMesh(GetMeshData(brickInfo), in sample, offset, in cachedBrick, _plate, new Orientation(orientationByte).ToQuaternion(), shapeLight.LightLevel);
                break;
            case BrickShape.Custom:
                if (cachedBrick.CustomMesh == null)
                {
                    ref readonly CubeMeshBuilder customFallbackBuilder = ref GetCubeMeshBuilder(brickInfo);
                    AddCube(in customFallbackBuilder, sample.Coords, offset, orientationByte, in sample, in cachedBrick, culledAbove, culledBelow, culledAhead, culledBehind, culledRight, culledLeft);
                    break;
                }

                AddMesh(GetMeshData(brickInfo), in sample, offset, in cachedBrick, cachedBrick.CustomMesh, new Orientation(orientationByte).ToQuaternion(), shapeLight.LightLevel);
                break;
            case BrickShape.Block:
            default:
                ref readonly CubeMeshBuilder cubeMeshBuilder = ref GetCubeMeshBuilder(brickInfo);
                AddCube(in cubeMeshBuilder, sample.Coords, offset, orientationByte, in sample, in cachedBrick, culledAbove, culledBelow, culledAhead, culledBehind, culledRight, culledLeft);
                break;
        }
    }
    
    private MeshState.MeshData GetMeshData(Brick brick)
    {
        return brick.Alpha switch
        {
            BrickAlpha.Masked => _meshState.Masked,
            BrickAlpha.Blend => _meshState.Transparent,
            _ => brick.CastShadows ? _meshState.Opaque : _meshState.OpaqueNoShadow,
        };
    }

    private ref readonly CubeMeshBuilder GetCubeMeshBuilder(Brick brick)
    {
        switch (brick.Alpha)
        {
            case BrickAlpha.Masked:
                return ref _maskedCubeMeshBuilder;
            case BrickAlpha.Blend:
                return ref _transparentCubeMeshBuilder;
            default:
                return ref brick.CastShadows ? ref _opaqueCubeMeshBuilder : ref _opaqueNoShadowCubeMeshBuilder;
        }
    }

    private bool IsCulledBy(in Voxel target, in Voxel neighbor)
    {
        if (neighbor.ID == 0)
        {
            return false;
        }
        
        //  Non-block shaped bricks of the same type cull one another 
        ShapeLight shapeLight = neighbor.ShapeLight;
        if (shapeLight.Shape != BrickShape.Block)
        {
            return false;
        }

        if (neighbor.ID == target.ID)
        {
            return true;
        }

        return _surfaceCache.GetBrick(neighbor.ID).IsCullerBlock;
    }
    
    private void AddCube(
        in CubeMeshBuilder cubeMeshBuilder,
        Int3 coords,
        Vector3 offset,
        byte orientationByte,
        in VoxelSample sample,
        in VoxelSurfaceCache.CachedBrick cachedBrick,
        bool culledAbove,
        bool culledBelow,
        bool culledAhead,
        bool culledBehind,
        bool culledRight,
        bool culledLeft
    ) {
        Vector3 origin = offset + new Vector3(coords.X, coords.Y, coords.Z);
        
        if (!culledAbove)
        {
            cubeMeshBuilder.AddTopFace(origin, orientationByte, in sample, in cachedBrick);
        }
        
        if (!culledBelow)
        {
            cubeMeshBuilder.AddBottomFace(origin, orientationByte, in sample, in cachedBrick);
        }
        
        if (!culledAhead)
        {
            cubeMeshBuilder.AddFrontFace(origin, orientationByte, in sample, in cachedBrick);
        }
        
        if (!culledBehind)
        {
            cubeMeshBuilder.AddBackFace(origin, orientationByte, in sample, in cachedBrick);
        }
        
        if (!culledRight)
        {
            cubeMeshBuilder.AddRightFace(origin, orientationByte, in sample, in cachedBrick);
        }
        
        if (!culledLeft)
        {
            cubeMeshBuilder.AddLeftFace(origin, orientationByte, in sample, in cachedBrick);
        }
    }

    private void AddMesh(
        MeshState.MeshData meshData,
        in VoxelSample sample,
        Vector3 offset,
        in VoxelSurfaceCache.CachedBrick cachedBrick,
        Mesh mesh,
        Quaternion orientation,
        int lightLevel
    ) {
        Vector3 origin = offset + new Vector3(sample.Coords.X, sample.Coords.Y, sample.Coords.Z);
        float light = Math.Clamp(lightLevel / 15f, 0.1f, 1f);

        ResolveFaceStyles(in sample, origin, in cachedBrick, out FaceTextureIndices faceTextures, out FaceColorTints faceTints);

        if (faceTints.HasAnyTint)
        {
            for (var i = 0; i < mesh.Colors.Length; i++)
            {
                Vector3 tint = i < mesh.Normals.Length
                    ? ResolveFaceTint(mesh.Normals[i], in faceTints)
                    : Vector3.One;
                meshData.Colors.Add(new Vector4(tint.X, tint.Y, tint.Z, light));
            }
        }
        else
        {
            var color = new Vector4(1f, 1f, 1f, light);
            for (var i = 0; i < mesh.Colors.Length; i++)
            {
                meshData.Colors.Add(color);
            }
        }

        if (faceTextures.HasPerFace)
        {
            for (var i = 0; i < mesh.Uv.Length; i++)
            {
                int textureIndex = i < mesh.Normals.Length
                    ? ResolveFaceTextureIndex(mesh.Normals[i], faceTextures)
                    : faceTextures.Default;
                meshData.UV.Add(mesh.Uv[i] with { Z = textureIndex });
            }
        }
        else
        {
            int textureIndex = faceTextures.Default;
            foreach (Vector3 texCoord in mesh.Uv)
            {
                meshData.UV.Add(texCoord with { Z = textureIndex });
            }
        }
        
        var vertexBase = (uint)meshData.Vertices.Count;
        foreach (uint tri in mesh.Triangles)
        {
            meshData.Triangles.Add(tri + vertexBase);
        }
        
        foreach (Vector3 normal in mesh.Normals)
        {
            meshData.Normals.Add(Vector3.Transform(normal, orientation));
        }
        
        foreach (Vector3 vertex in mesh.Vertices)
        {
            meshData.Vertices.Add(Vector3.Transform(vertex, orientation) + origin);
        }
    }

    private void ResolveFaceStyles(
        in VoxelSample sample,
        Vector3 origin,
        in VoxelSurfaceCache.CachedBrick cachedBrick,
        out FaceTextureIndices faceTextures,
        out FaceColorTints faceTints
    ) {
        int defaultIndex = cachedBrick.DefaultTextures.ResolveRequiredLayerIndex(origin);
        bool hasOverrides = sample.Center.HasFaceOverrides() && sample.VoxelIndex >= 0;

        if (!hasOverrides && !cachedBrick.HasPerFaceTextures)
        {
            faceTextures = new FaceTextureIndices(defaultIndex, -1, -1, -1, -1, -1, -1);
            faceTints = new FaceColorTints(Vector3.One, Vector3.One, Vector3.One, Vector3.One, Vector3.One, Vector3.One, hasAnyTint: false);
            return;
        }

        int topIndex = cachedBrick.TopTextures.ResolveOptionalLayerIndex(origin);
        int bottomIndex = cachedBrick.BottomTextures.ResolveOptionalLayerIndex(origin);
        int frontIndex = cachedBrick.FrontTextures.ResolveOptionalLayerIndex(origin);
        int backIndex = cachedBrick.BackTextures.ResolveOptionalLayerIndex(origin);
        int leftIndex = cachedBrick.LeftTextures.ResolveOptionalLayerIndex(origin);
        int rightIndex = cachedBrick.RightTextures.ResolveOptionalLayerIndex(origin);

        Vector3 topTint = Vector3.One;
        Vector3 bottomTint = Vector3.One;
        Vector3 frontTint = Vector3.One;
        Vector3 backTint = Vector3.One;
        Vector3 leftTint = Vector3.One;
        Vector3 rightTint = Vector3.One;
        var hasAnyTint = false;

        if (hasOverrides)
        {
            ApplyFaceOverride(in sample, origin, BrickFace.Top, ref topIndex, ref topTint, ref hasAnyTint);
            ApplyFaceOverride(in sample, origin, BrickFace.Bottom, ref bottomIndex, ref bottomTint, ref hasAnyTint);
            ApplyFaceOverride(in sample, origin, BrickFace.Front, ref frontIndex, ref frontTint, ref hasAnyTint);
            ApplyFaceOverride(in sample, origin, BrickFace.Back, ref backIndex, ref backTint, ref hasAnyTint);
            ApplyFaceOverride(in sample, origin, BrickFace.Left, ref leftIndex, ref leftTint, ref hasAnyTint);
            ApplyFaceOverride(in sample, origin, BrickFace.Right, ref rightIndex, ref rightTint, ref hasAnyTint);
        }

        faceTextures = new FaceTextureIndices(
            defaultIndex: defaultIndex,
            topIndex: topIndex,
            bottomIndex: bottomIndex,
            frontIndex: frontIndex,
            backIndex: backIndex,
            leftIndex: leftIndex,
            rightIndex: rightIndex
        );

        faceTints = new FaceColorTints(topTint, bottomTint, frontTint, backTint, leftTint, rightTint, hasAnyTint);
    }

    private void ApplyFaceOverride(
        in VoxelSample sample,
        Vector3 origin,
        BrickFace face,
        ref int textureIndex,
        ref Vector3 tint,
        ref bool hasAnyTint
    ) {
        if (!sample.ChunkData.TryGetFaceOverride(sample.VoxelIndex, face, out FaceOverride faceOverride))
        {
            return;
        }

        if (faceOverride.TexturePaintId != 0)
        {
            ref readonly VoxelSurfaceCache.CachedPaint texturePaint = ref _surfaceCache.GetPaint(faceOverride.TexturePaintId);
            textureIndex = texturePaint.Exists && texturePaint.Kind == PaintKind.Texture
                ? texturePaint.Textures.ResolveRequiredLayerIndex(origin)
                : 0;
        }

        if (faceOverride.ColorPaintId != 0)
        {
            ref readonly VoxelSurfaceCache.CachedPaint colorPaint = ref _surfaceCache.GetPaint(faceOverride.ColorPaintId);
            if (colorPaint.Exists && colorPaint.Kind == PaintKind.Color)
            {
                tint = colorPaint.LinearColor;
                hasAnyTint = true;
            }
        }
    }

    private static Vector3 ResolveFaceTint(in Vector3 normal, in FaceColorTints tints)
    {
        float ax = MathF.Abs(normal.X);
        float ay = MathF.Abs(normal.Y);
        float az = MathF.Abs(normal.Z);
        if (ax <= NORMAL_EPSILON && ay <= NORMAL_EPSILON && az <= NORMAL_EPSILON)
        {
            return Vector3.One;
        }

        return BrickFaceExtensions.FromLocalNormal(normal) switch
        {
            BrickFace.Top => tints.Top,
            BrickFace.Bottom => tints.Bottom,
            BrickFace.Front => tints.Front,
            BrickFace.Back => tints.Back,
            BrickFace.Left => tints.Left,
            BrickFace.Right => tints.Right,
            _ => Vector3.One,
        };
    }

    private static int ResolveFaceTextureIndex(in Vector3 normal, in FaceTextureIndices indices)
    {
        float ax = MathF.Abs(normal.X);
        float ay = MathF.Abs(normal.Y);
        float az = MathF.Abs(normal.Z);
        if (ax <= NORMAL_EPSILON && ay <= NORMAL_EPSILON && az <= NORMAL_EPSILON)
        {
            return indices.Default;
        }

        //  Start in tie-break priority order (Z, Y, X) so 45-degree slopes prefer Front, then Top.
        float m0 = az;
        int i0 = az > NORMAL_EPSILON ? (normal.Z >= 0f ? indices.Front : indices.Back) : -1;
        float m1 = ay;
        int i1 = ay > NORMAL_EPSILON ? (normal.Y >= 0f ? indices.Top : indices.Bottom) : -1;
        float m2 = ax;
        int i2 = ax > NORMAL_EPSILON ? (normal.X >= 0f ? indices.Right : indices.Left) : -1;

        if (m1 > m0 + NORMAL_EPSILON)
        {
            (m0, m1) = (m1, m0);
            (i0, i1) = (i1, i0);
        }

        if (m2 > m1 + NORMAL_EPSILON)
        {
            (m1, m2) = (m2, m1);
            (i1, i2) = (i2, i1);
        }

        if (m1 > m0 + NORMAL_EPSILON)
        {
            (m0, m1) = (m1, m0);
            (i0, i1) = (i1, i0);
        }

        if (i0 >= 0)
        {
            return i0;
        }

        float minSecondary = m0 * SECONDARY_AXIS_RATIO;
        if (i1 >= 0 && m1 >= minSecondary)
        {
            return i1;
        }

        if (i2 >= 0 && m2 >= minSecondary)
        {
            return i2;
        }

        return indices.Default;
    }
    
    private readonly struct FaceTextureIndices(
        int defaultIndex,
        int topIndex,
        int bottomIndex,
        int frontIndex,
        int backIndex,
        int leftIndex,
        int rightIndex
    ) {
        public readonly int Default = defaultIndex;
        public readonly int Top = topIndex;
        public readonly int Bottom = bottomIndex;
        public readonly int Front = frontIndex;
        public readonly int Back = backIndex;
        public readonly int Left = leftIndex;
        public readonly int Right = rightIndex;
        public bool HasPerFace => Top >= 0 || Bottom >= 0 || Front >= 0 || Back >= 0 || Left >= 0 || Right >= 0;
    }

    private readonly struct FaceColorTints(
        Vector3 top,
        Vector3 bottom,
        Vector3 front,
        Vector3 back,
        Vector3 left,
        Vector3 right,
        bool hasAnyTint
    ) {
        public readonly Vector3 Top = top;
        public readonly Vector3 Bottom = bottom;
        public readonly Vector3 Front = front;
        public readonly Vector3 Back = back;
        public readonly Vector3 Left = left;
        public readonly Vector3 Right = right;
        public readonly bool HasAnyTint = hasAnyTint;
    }
}
