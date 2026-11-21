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

internal sealed class MeshPostPass(
    MeshState meshState,
    IBrickDatabase brickDatabase,
    PBRTextureArrays textureArrays,
    IAssetDatabase<Mesh> meshDatabase,
    IPaintDatabase? paintDatabase = null
) : VoxelObjectProcessor.ISamplePass
{
    private const float NORMAL_EPSILON = 1e-4f;
    private const float SECONDARY_AXIS_RATIO = 0.5f;
    
    private static readonly Brick _unresolvedBrick = new(
        id: string.Empty,
        alpha: BrickAlpha.Opaque,
        passable: false,
        meshID: null,
        shape: BrickShape.Any,
        textures: default,
        tags: null
    );

    private readonly CubeMeshBuilder _opaqueCubeMeshBuilder = new(textureArrays.Albedo, meshState.Opaque, brickDatabase, paintDatabase);
    private readonly CubeMeshBuilder _opaqueNoShadowCubeMeshBuilder = new(textureArrays.Albedo, meshState.OpaqueNoShadow, brickDatabase, paintDatabase);
    private readonly CubeMeshBuilder _maskedCubeMeshBuilder = new(textureArrays.Albedo, meshState.Masked, brickDatabase, paintDatabase);
    private readonly CubeMeshBuilder _transparentCubeMeshBuilder = new(textureArrays.Albedo, meshState.Transparent, brickDatabase, paintDatabase);
    private readonly Mesh _slope = meshDatabase.Get("slope.obj").Value;
    private readonly Mesh _stair = meshDatabase.Get("stair.obj").Value;
    private readonly Mesh _slab = meshDatabase.Get("slab.obj").Value;
    private readonly Mesh _column = meshDatabase.Get("column.obj").Value;
    private readonly Mesh _plate = meshDatabase.Get("plate.obj").Value;
    
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
        
        Result<Brick> brickInfoResult = brickDatabase.Get(sample.Center.ID);
        Brick brickInfo = brickInfoResult.Success ? brickInfoResult.Value : _unresolvedBrick;
        
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
        
        var orientation = sample.Center.GetOrientation().ToQuaternion();
        ShapeLight shapeLight = sample.Center.ShapeLight;
        var offset = new Vector3(sample.ChunkOffset.X, sample.ChunkOffset.Y, sample.ChunkOffset.Z);
        
        MeshState.MeshData targetMesh = GetMeshData(brickInfo);
        ref readonly CubeMeshBuilder cubeMeshBuilder = ref GetCubeMeshBuilder(brickInfo);

        switch (shapeLight.Shape)
        {
            case BrickShape.Slab:
                AddMesh(targetMesh, in sample, offset, brickInfo, _slab, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Stair:
                AddMesh(targetMesh, in sample, offset, brickInfo, _stair, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Slope:
                AddMesh(targetMesh, in sample, offset, brickInfo, _slope, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Column:
                AddMesh(targetMesh, in sample, offset, brickInfo, _column, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Plate:
                AddMesh(targetMesh, in sample, offset, brickInfo, _plate, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Custom:
                Mesh? customMesh = ResolveMesh(in brickInfo);
                if (customMesh == null)
                {
                    AddCube(cubeMeshBuilder, sample.Coords, offset, orientation, sample, brickInfo, culledAbove, culledBelow, culledAhead, culledBehind, culledRight, culledLeft);
                    break;
                }

                AddMesh(targetMesh, in sample, offset, brickInfo, customMesh, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Block:
            default:
                AddCube(cubeMeshBuilder, sample.Coords, offset, orientation, sample, brickInfo, culledAbove, culledBelow, culledAhead, culledBehind, culledRight, culledLeft);
                break;
        }
    }
    
    private MeshState.MeshData GetMeshData(Brick brick)
    {
        return brick.Alpha switch
        {
            BrickAlpha.Masked => meshState.Masked,
            BrickAlpha.Blend => meshState.Transparent,
            _ => brick.CastShadows ? meshState.Opaque : meshState.OpaqueNoShadow,
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
        if (neighbor.ID == target.ID && shapeLight.Shape == BrickShape.Block)
        {
            return true;
        }
        
        return brickDatabase.IsCuller(neighbor, shapeLight.Shape);
    }

    /// <summary>Resolves a custom brick's mesh id to a renderable mesh, or null when absent or missing.</summary>
    private Mesh? ResolveMesh(in Brick brickInfo)
    {
        if (brickInfo.MeshID == null)
        {
            return null;
        }

        Result<Mesh> meshResult = meshDatabase.Get(brickInfo.MeshID);
        return meshResult.Success ? meshResult.Value : null;
    }
    
    private void AddCube(
        in CubeMeshBuilder cubeMeshBuilder,
        Int3 coords,
        Vector3 offset,
        Quaternion orientation,
        in VoxelSample sample,
        Brick brickInfo,
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
            cubeMeshBuilder.AddTopFace(origin, orientation, sample, brickInfo);
        }
        
        if (!culledBelow)
        {
            cubeMeshBuilder.AddBottomFace(origin, orientation, sample, brickInfo);
        }
        
        if (!culledAhead)
        {
            cubeMeshBuilder.AddFrontFace(origin, orientation, sample, brickInfo);
        }
        
        if (!culledBehind)
        {
            cubeMeshBuilder.AddBackFace(origin, orientation, sample, brickInfo);
        }
        
        if (!culledRight)
        {
            cubeMeshBuilder.AddRightFace(origin, orientation, sample, brickInfo);
        }
        
        if (!culledLeft)
        {
            cubeMeshBuilder.AddLeftFace(origin, orientation, sample, brickInfo);
        }
    }

    private void AddMesh(MeshState.MeshData meshData, in VoxelSample sample, Vector3 offset, Brick brickInfo, Mesh mesh, Quaternion orientation, int lightLevel)
    {
        Vector3 origin = offset + new Vector3(sample.Coords.X, sample.Coords.Y, sample.Coords.Z);
        float light = Math.Clamp(lightLevel / 15f, 0.1f, 1f);

        ResolveFaceStyles(in sample, origin, brickInfo.Textures, out FaceTextureIndices faceTextures, out FaceColorTints faceTints);

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
        
        foreach (uint tri in mesh.Triangles)
        {
            meshData.Triangles.Add(tri + (uint)meshData.Vertices.Count);
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
        in BrickTextures textures,
        out FaceTextureIndices faceTextures,
        out FaceColorTints faceTints
    ) {
        int defaultIndex = ResolveOptionalTextureIndex(origin, textures.Default);
        int topIndex = ResolveOptionalTextureIndex(origin, textures.Top);
        int bottomIndex = ResolveOptionalTextureIndex(origin, textures.Bottom);
        int frontIndex = ResolveOptionalTextureIndex(origin, textures.Front);
        int backIndex = ResolveOptionalTextureIndex(origin, textures.Back);
        int leftIndex = ResolveOptionalTextureIndex(origin, textures.Left);
        int rightIndex = ResolveOptionalTextureIndex(origin, textures.Right);

        Vector3 topTint = Vector3.One;
        Vector3 bottomTint = Vector3.One;
        Vector3 frontTint = Vector3.One;
        Vector3 backTint = Vector3.One;
        Vector3 leftTint = Vector3.One;
        Vector3 rightTint = Vector3.One;
        var hasAnyTint = false;

        if (sample.Center.HasFaceOverrides() && sample.VoxelIndex >= 0)
        {
            ApplyFaceOverride(in sample, origin, BrickFace.Top, ref topIndex, ref topTint, ref hasAnyTint);
            ApplyFaceOverride(in sample, origin, BrickFace.Bottom, ref bottomIndex, ref bottomTint, ref hasAnyTint);
            ApplyFaceOverride(in sample, origin, BrickFace.Front, ref frontIndex, ref frontTint, ref hasAnyTint);
            ApplyFaceOverride(in sample, origin, BrickFace.Back, ref backIndex, ref backTint, ref hasAnyTint);
            ApplyFaceOverride(in sample, origin, BrickFace.Left, ref leftIndex, ref leftTint, ref hasAnyTint);
            ApplyFaceOverride(in sample, origin, BrickFace.Right, ref rightIndex, ref rightTint, ref hasAnyTint);
        }

        faceTextures = new FaceTextureIndices(
            defaultIndex: defaultIndex >= 0 ? defaultIndex : 0,
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
            textureIndex = ResolvePaintTextureIndex(origin, faceOverride.TexturePaintId);
        }

        if (faceOverride.ColorPaintId != 0 && paintDatabase != null)
        {
            Result<Paint> colorResult = paintDatabase.Get(faceOverride.ColorPaintId);
            if (colorResult.Success && colorResult.Value.Kind == PaintKind.Color)
            {
                tint = colorResult.Value.LinearColor;
                hasAnyTint = true;
            }
        }
    }

    private int ResolvePaintTextureIndex(Vector3 origin, ushort paintId)
    {
        if (paintDatabase == null)
        {
            return 0;
        }

        Result<Paint> result = paintDatabase.Get(paintId);
        if (!result.Success || result.Value.Kind != PaintKind.Texture)
        {
            return 0;
        }

        string? textureName = CubeMeshBuilder.GetTextureName(origin, result.Value.Textures);
        return textureName != null ? Math.Max(textureArrays.Albedo.IndexOf(textureName), 0) : 0;
    }

    private int ResolveOptionalTextureIndex(Vector3 origin, string?[]? textures)
    {
        string? textureName = CubeMeshBuilder.GetTextureName(origin, textures);
        return textureName != null ? Math.Max(textureArrays.Albedo.IndexOf(textureName), 0) : -1;
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
