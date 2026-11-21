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
    IAssetDatabase<Mesh> meshDatabase
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

    private readonly CubeMeshBuilder _opaqueCubeMeshBuilder = new(textureArrays.Albedo, meshState.Opaque);
    private readonly CubeMeshBuilder _opaqueNoShadowCubeMeshBuilder = new(textureArrays.Albedo, meshState.OpaqueNoShadow);
    private readonly CubeMeshBuilder _maskedCubeMeshBuilder = new(textureArrays.Albedo, meshState.Masked);
    private readonly CubeMeshBuilder _transparentCubeMeshBuilder = new(textureArrays.Albedo, meshState.Transparent);
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
        
        var orientation = ((Orientation)sample.Center.Orientation).ToQuaternion();
        ShapeLight shapeLight = sample.Center.ShapeLight;
        var offset = new Vector3(sample.ChunkOffset.X, sample.ChunkOffset.Y, sample.ChunkOffset.Z);
        
        MeshState.MeshData targetMesh = GetMeshData(brickInfo);
        ref readonly CubeMeshBuilder cubeMeshBuilder = ref GetCubeMeshBuilder(brickInfo);

        switch (shapeLight.Shape)
        {
            case BrickShape.Slab:
                AddMesh(targetMesh, sample.Coords, offset, brickInfo, _slab, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Stair:
                AddMesh(targetMesh, sample.Coords, offset, brickInfo, _stair, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Slope:
                AddMesh(targetMesh, sample.Coords, offset, brickInfo, _slope, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Column:
                AddMesh(targetMesh, sample.Coords, offset, brickInfo, _column, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Plate:
                AddMesh(targetMesh, sample.Coords, offset, brickInfo, _plate, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Custom:
                Mesh? customMesh = ResolveMesh(in brickInfo);
                if (customMesh == null)
                {
                    AddCube(cubeMeshBuilder, sample.Coords, offset, orientation, sample, brickInfo, culledAbove, culledBelow, culledAhead, culledBehind, culledRight, culledLeft);
                    break;
                }

                AddMesh(targetMesh, sample.Coords, offset, brickInfo, customMesh, orientation, shapeLight.LightLevel);
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

    private void AddMesh(MeshState.MeshData meshData, Int3 coords, Vector3 offset, Brick brickInfo, Mesh mesh, Quaternion orientation, int lightLevel)
    {
        float light = Math.Clamp(lightLevel / 15f, 0.1f, 1f);
        var color = new Vector4(light, light, light, 1f);
        for (var i = 0; i < mesh.Colors.Length; i++)
        {
            meshData.Colors.Add(color);
        }

        Vector3 origin = offset + new Vector3(coords.X, coords.Y, coords.Z);
        FaceTextureIndices faceTextures = ResolveFaceTextureIndices(origin, brickInfo.Textures);
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

    private FaceTextureIndices ResolveFaceTextureIndices(Vector3 origin, in BrickTextures textures)
    {
        int defaultIndex = ResolveOptionalTextureIndex(origin, textures.Default);
        return new FaceTextureIndices(
            defaultIndex: defaultIndex >= 0 ? defaultIndex : 0,
            topIndex: ResolveOptionalTextureIndex(origin, textures.Top),
            bottomIndex: ResolveOptionalTextureIndex(origin, textures.Bottom),
            frontIndex: ResolveOptionalTextureIndex(origin, textures.Front),
            backIndex: ResolveOptionalTextureIndex(origin, textures.Back),
            leftIndex: ResolveOptionalTextureIndex(origin, textures.Left),
            rightIndex: ResolveOptionalTextureIndex(origin, textures.Right)
        );
    }

    private int ResolveOptionalTextureIndex(Vector3 origin, string?[]? textures)
    {
        string? textureName = CubeMeshBuilder.GetTextureName(origin, textures);
        return textureName != null ? Math.Max(textureArrays.Albedo.IndexOf(textureName), 0) : -1;
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
}