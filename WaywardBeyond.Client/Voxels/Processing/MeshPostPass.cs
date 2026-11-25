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
    private const float BOUNDARY_EPSILON = 1e-3f;
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
    private readonly ConnectedTextureResolver _connectedTextureResolver;
    private readonly CubeMeshBuilder _opaqueCubeMeshBuilder;
    private readonly CubeMeshBuilder _opaqueNoShadowCubeMeshBuilder;
    private readonly CubeMeshBuilder _maskedCubeMeshBuilder;
    private readonly CubeMeshBuilder _transparentCubeMeshBuilder;
    private readonly CullableMesh _slope;
    private readonly CullableMesh _stair;
    private readonly CullableMesh _slab;
    private readonly CullableMesh _column;
    private readonly CullableMesh _plate;

    public MeshPostPass(
        MeshState meshState,
        IBrickDatabase brickDatabase,
        PBRTextureArrays textureArrays,
        IAssetDatabase<Mesh> meshDatabase,
        IPaintDatabase? paintDatabase = null
    ) {
        _meshState = meshState;
        _surfaceCache = new VoxelSurfaceCache(brickDatabase, textureArrays.Albedo, meshDatabase, paintDatabase);
        _connectedTextureResolver = new ConnectedTextureResolver(_surfaceCache);
        _opaqueCubeMeshBuilder = new CubeMeshBuilder(meshState.Opaque, _connectedTextureResolver);
        _opaqueNoShadowCubeMeshBuilder = new CubeMeshBuilder(meshState.OpaqueNoShadow, _connectedTextureResolver);
        _maskedCubeMeshBuilder = new CubeMeshBuilder(meshState.Masked, _connectedTextureResolver);
        _transparentCubeMeshBuilder = new CubeMeshBuilder(meshState.Transparent, _connectedTextureResolver);
        _slope = new CullableMesh(meshDatabase.Get("slope.obj").Value);
        _stair = new CullableMesh(meshDatabase.Get("stair.obj").Value);
        _slab = new CullableMesh(meshDatabase.Get("slab.obj").Value);
        _column = new CullableMesh(meshDatabase.Get("column.obj").Value);
        _plate = new CullableMesh(meshDatabase.Get("plate.obj").Value);
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
        
        BrickFaceMask blockCulledFaces = BrickFaceMask.None;
        if (IsCulledBy(target: sample.Center, neighbor: sample.Above)) blockCulledFaces |= BrickFaceMask.Top;
        if (IsCulledBy(target: sample.Center, neighbor: sample.Below)) blockCulledFaces |= BrickFaceMask.Bottom;
        if (IsCulledBy(target: sample.Center, neighbor: sample.Ahead)) blockCulledFaces |= BrickFaceMask.Front;
        if (IsCulledBy(target: sample.Center, neighbor: sample.Behind)) blockCulledFaces |= BrickFaceMask.Back;
        if (IsCulledBy(target: sample.Center, neighbor: sample.Left)) blockCulledFaces |= BrickFaceMask.Left;
        if (IsCulledBy(target: sample.Center, neighbor: sample.Right)) blockCulledFaces |= BrickFaceMask.Right;

        if (blockCulledFaces == BrickFaceMask.All)
        {
            return;
        }
        
        byte orientationByte = sample.Center.GetOrientation().ToByte();
        ShapeLight shapeLight = sample.Center.ShapeLight;
        var offset = new Vector3(sample.ChunkOffset.X, sample.ChunkOffset.Y, sample.ChunkOffset.Z);

        switch (shapeLight.Shape)
        {
            case BrickShape.Slab:
                AddMesh(GetMeshData(brickInfo), in sample, offset, orientationByte, shapeLight, in cachedBrick, _slab, GetNonBlockCulledFaces(in sample, orientationByte, shapeLight.Shape, blockCulledFaces));
                break;
            case BrickShape.Stair:
                AddMesh(GetMeshData(brickInfo), in sample, offset, orientationByte, shapeLight, in cachedBrick, _stair, GetNonBlockCulledFaces(in sample, orientationByte, shapeLight.Shape, blockCulledFaces));
                break;
            case BrickShape.Slope:
                AddMesh(GetMeshData(brickInfo), in sample, offset, orientationByte, shapeLight, in cachedBrick, _slope, GetNonBlockCulledFaces(in sample, orientationByte, shapeLight.Shape, blockCulledFaces));
                break;
            case BrickShape.Column:
                AddMesh(GetMeshData(brickInfo), in sample, offset, orientationByte, shapeLight, in cachedBrick, _column, GetNonBlockCulledFaces(in sample, orientationByte, shapeLight.Shape, blockCulledFaces));
                break;
            case BrickShape.Plate:
                AddMesh(GetMeshData(brickInfo), in sample, offset, orientationByte, shapeLight, in cachedBrick, _plate, GetNonBlockCulledFaces(in sample, orientationByte, shapeLight.Shape, blockCulledFaces));
                break;
            case BrickShape.Custom:
                if (cachedBrick.CustomMesh == null)
                {
                    ref readonly CubeMeshBuilder customFallbackBuilder = ref GetCubeMeshBuilder(brickInfo);
                    AddCube(in customFallbackBuilder, sample.Coords, offset, orientationByte, in sample, in cachedBrick, blockCulledFaces);
                    break;
                }

                AddMesh(GetMeshData(brickInfo), in sample, offset, orientationByte, shapeLight, in cachedBrick, new CullableMesh(cachedBrick.CustomMesh, triangleFaces: null), BrickFaceMask.None);
                break;
            case BrickShape.Block:
            default:
                ref readonly CubeMeshBuilder cubeMeshBuilder = ref GetCubeMeshBuilder(brickInfo);
                AddCube(in cubeMeshBuilder, sample.Coords, offset, orientationByte, in sample, in cachedBrick, blockCulledFaces);
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
        BrickFaceMask culledFaces
    ) {
        Vector3 origin = offset + new Vector3(coords.X, coords.Y, coords.Z);

        if ((culledFaces & BrickFaceMask.Top) == 0)
        {
            cubeMeshBuilder.AddTopFace(origin, orientationByte, in sample, in cachedBrick);
        }

        if ((culledFaces & BrickFaceMask.Bottom) == 0)
        {
            cubeMeshBuilder.AddBottomFace(origin, orientationByte, in sample, in cachedBrick);
        }

        if ((culledFaces & BrickFaceMask.Front) == 0)
        {
            cubeMeshBuilder.AddFrontFace(origin, orientationByte, in sample, in cachedBrick);
        }

        if ((culledFaces & BrickFaceMask.Back) == 0)
        {
            cubeMeshBuilder.AddBackFace(origin, orientationByte, in sample, in cachedBrick);
        }

        if ((culledFaces & BrickFaceMask.Right) == 0)
        {
            cubeMeshBuilder.AddRightFace(origin, orientationByte, in sample, in cachedBrick);
        }

        if ((culledFaces & BrickFaceMask.Left) == 0)
        {
            cubeMeshBuilder.AddLeftFace(origin, orientationByte, in sample, in cachedBrick);
        }
    }

    private BrickFaceMask GetNonBlockCulledFaces(
        in VoxelSample sample,
        byte centerOrientationByte,
        BrickShape shape,
        BrickFaceMask blockCulledFaces
    ) {
        BrickFaceMask mask = blockCulledFaces;
        BrickFaceMask sameShapeFaces = GetSameShapeCullableFaces(shape) & ~blockCulledFaces;
        if (sameShapeFaces == BrickFaceMask.None)
        {
            return mask;
        }

        var centerOrientation = new Orientation(centerOrientationByte);

        if ((sameShapeFaces & BrickFaceMask.Top) != 0 && IsCulledBySameShape(in sample.Center, centerOrientation, shape, in sample.Above))
        {
            mask |= BrickFaceMask.Top;
        }

        if ((sameShapeFaces & BrickFaceMask.Bottom) != 0 && IsCulledBySameShape(in sample.Center, centerOrientation, shape, in sample.Below))
        {
            mask |= BrickFaceMask.Bottom;
        }

        if ((sameShapeFaces & BrickFaceMask.Front) != 0 && IsCulledBySameShape(in sample.Center, centerOrientation, shape, in sample.Ahead))
        {
            mask |= BrickFaceMask.Front;
        }

        if ((sameShapeFaces & BrickFaceMask.Back) != 0 && IsCulledBySameShape(in sample.Center, centerOrientation, shape, in sample.Behind))
        {
            mask |= BrickFaceMask.Back;
        }

        if ((sameShapeFaces & BrickFaceMask.Left) != 0 && IsCulledBySameShape(in sample.Center, centerOrientation, shape, in sample.Left))
        {
            mask |= BrickFaceMask.Left;
        }

        if ((sameShapeFaces & BrickFaceMask.Right) != 0 && IsCulledBySameShape(in sample.Center, centerOrientation, shape, in sample.Right))
        {
            mask |= BrickFaceMask.Right;
        }

        return mask;
    }

    private bool IsCulledBySameShape(in Voxel target, Orientation centerOrientation, BrickShape shape, in Voxel neighbor)
    {
        if (neighbor.ID == 0)
        {
            return false;
        }

        ShapeLight neighborShapeLight = neighbor.ShapeLight;
        if (neighborShapeLight.Shape != shape)
        {
            return false;
        }

        if (!ConnectedTextureResolver.AreOrientationsCompatible(shape, centerOrientation, neighbor.GetOrientation()))
        {
            return false;
        }

        return neighbor.ID == target.ID || _surfaceCache.GetBrick(neighbor.ID).IsCullerBlock;
    }

    private static BrickFaceMask GetSameShapeCullableFaces(BrickShape shape)
    {
        return shape switch
        {
            BrickShape.Slab or BrickShape.Plate => BrickFaceMask.Left | BrickFaceMask.Right | BrickFaceMask.Front | BrickFaceMask.Back,
            BrickShape.Slope => BrickFaceMask.Left | BrickFaceMask.Right,
            BrickShape.Stair => BrickFaceMask.Left | BrickFaceMask.Right | BrickFaceMask.Top | BrickFaceMask.Front,
            BrickShape.Column => BrickFaceMask.Top | BrickFaceMask.Bottom,
            _ => BrickFaceMask.None,
        };
    }

    private void AddMesh(
        MeshState.MeshData meshData,
        in VoxelSample sample,
        Vector3 offset,
        byte orientationByte,
        ShapeLight shapeLight,
        in VoxelSurfaceCache.CachedBrick cachedBrick,
        in CullableMesh cullableMesh,
        BrickFaceMask culledFaces
    ) {
        Mesh mesh = cullableMesh.Mesh;
        BrickFaceMask effectiveCulledFaces = culledFaces & cullableMesh.BoundaryFaces;
        BrickFaceMask fullyCulledFaces = effectiveCulledFaces & cullableMesh.FullBoundaryFaces;
        Vector3 origin = offset + new Vector3(sample.Coords.X, sample.Coords.Y, sample.Coords.Z);
        Quaternion orientation = new Orientation(orientationByte).ToQuaternion();
        float light = Math.Clamp(shapeLight.LightLevel / 15f, 0.1f, 1f);

        ResolveFaceStyles(in sample, origin, orientationByte, shapeLight.Shape, in cachedBrick, fullyCulledFaces, out FaceTextureIndices faceTextures, out FaceColorTints faceTints);

        BrickFaceMask[]? triangleFaces = cullableMesh.TriangleFaces;
        if (effectiveCulledFaces != BrickFaceMask.None && triangleFaces != null)
        {
            AppendFilteredMesh(meshData, in cullableMesh, triangleFaces, effectiveCulledFaces, origin, orientation, light, in faceTextures, in faceTints);
            return;
        }

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

    private static void AppendFilteredMesh(
        MeshState.MeshData meshData,
        in CullableMesh cullableMesh,
        BrickFaceMask[] triangleFaces,
        BrickFaceMask culledFaces,
        Vector3 origin,
        Quaternion orientation,
        float light,
        in FaceTextureIndices faceTextures,
        in FaceColorTints faceTints
    ) {
        Mesh mesh = cullableMesh.Mesh;
        bool uniformNormals = cullableMesh.HasUniformTriangleNormals;
        int triangleCount = triangleFaces.Length;

        for (var t = 0; t < triangleCount; t++)
        {
            if ((triangleFaces[t] & culledFaces) != 0)
            {
                continue;
            }

            int triOffset = t * 3;
            int i0 = (int)mesh.Triangles[triOffset + 0];
            int i1 = (int)mesh.Triangles[triOffset + 1];
            int i2 = (int)mesh.Triangles[triOffset + 2];
            var vertexBase = (uint)meshData.Vertices.Count;

            if (uniformNormals && i0 < mesh.Normals.Length)
            {
                Vector3 localNormal = mesh.Normals[i0];
                Vector3 tint = faceTints.HasAnyTint ? ResolveFaceTint(localNormal, in faceTints) : Vector3.One;
                int textureIndex = faceTextures.HasPerFace ? ResolveFaceTextureIndex(localNormal, faceTextures) : faceTextures.Default;
                var color = new Vector4(tint.X, tint.Y, tint.Z, light);
                Vector3 worldNormal = Vector3.Transform(localNormal, orientation);

                meshData.Colors.Add(color);
                meshData.Colors.Add(color);
                meshData.Colors.Add(color);

                meshData.UV.Add(mesh.Uv[i0] with { Z = textureIndex });
                meshData.UV.Add(mesh.Uv[i1] with { Z = textureIndex });
                meshData.UV.Add(mesh.Uv[i2] with { Z = textureIndex });

                meshData.Normals.Add(worldNormal);
                meshData.Normals.Add(worldNormal);
                meshData.Normals.Add(worldNormal);

                meshData.Vertices.Add(Vector3.Transform(mesh.Vertices[i0], orientation) + origin);
                meshData.Vertices.Add(Vector3.Transform(mesh.Vertices[i1], orientation) + origin);
                meshData.Vertices.Add(Vector3.Transform(mesh.Vertices[i2], orientation) + origin);
            }
            else
            {
                AppendMeshVertex(meshData, mesh, i0, origin, orientation, light, in faceTextures, in faceTints);
                AppendMeshVertex(meshData, mesh, i1, origin, orientation, light, in faceTextures, in faceTints);
                AppendMeshVertex(meshData, mesh, i2, origin, orientation, light, in faceTextures, in faceTints);
            }

            meshData.Triangles.Add(vertexBase + 0);
            meshData.Triangles.Add(vertexBase + 1);
            meshData.Triangles.Add(vertexBase + 2);
        }
    }

    private static void AppendMeshVertex(
        MeshState.MeshData meshData,
        Mesh mesh,
        int index,
        Vector3 origin,
        Quaternion orientation,
        float light,
        in FaceTextureIndices faceTextures,
        in FaceColorTints faceTints
    ) {
        Vector3 localNormal = index < mesh.Normals.Length ? mesh.Normals[index] : Vector3.Zero;
        Vector3 tint = faceTints.HasAnyTint && index < mesh.Normals.Length
            ? ResolveFaceTint(localNormal, in faceTints)
            : Vector3.One;
        int textureIndex = faceTextures.HasPerFace && index < mesh.Normals.Length
            ? ResolveFaceTextureIndex(localNormal, faceTextures)
            : faceTextures.Default;

        meshData.Colors.Add(new Vector4(tint.X, tint.Y, tint.Z, light));
        meshData.UV.Add(mesh.Uv[index] with { Z = textureIndex });
        meshData.Normals.Add(Vector3.Transform(localNormal, orientation));
        meshData.Vertices.Add(Vector3.Transform(mesh.Vertices[index], orientation) + origin);
    }

    private void ResolveFaceStyles(
        in VoxelSample sample,
        Vector3 origin,
        byte orientationByte,
        BrickShape shape,
        in VoxelSurfaceCache.CachedBrick cachedBrick,
        BrickFaceMask fullyCulledFaces,
        out FaceTextureIndices faceTextures,
        out FaceColorTints faceTints
    ) {
        int defaultIndex = cachedBrick.DefaultTextures.ResolveRequiredLayerIndex(origin);
        bool hasOverrides = sample.Center.HasFaceOverrides() && sample.VoxelIndex >= 0;
        bool supportsConnected = ConnectedTextureResolver.SupportsConnectedTextures(shape);

        if (!hasOverrides && !cachedBrick.HasPerFaceTextures && !(cachedBrick.Connected && supportsConnected))
        {
            faceTextures = new FaceTextureIndices(defaultIndex, -1, -1, -1, -1, -1, -1);
            Vector3 defaultTint = cachedBrick.HasDefaultTint ? cachedBrick.DefaultTint : Vector3.One;
            faceTints = new FaceColorTints(defaultTint, defaultTint, defaultTint, defaultTint, defaultTint, defaultTint, cachedBrick.HasDefaultTint);
            return;
        }

        var hasAnyTint = false;
        int topIndex = ResolveMeshFaceStyle(in sample, origin, orientationByte, shape, in cachedBrick, BrickFace.Top, fullyCulledFaces, out Vector3 topTint, ref hasAnyTint);
        int bottomIndex = ResolveMeshFaceStyle(in sample, origin, orientationByte, shape, in cachedBrick, BrickFace.Bottom, fullyCulledFaces, out Vector3 bottomTint, ref hasAnyTint);
        int frontIndex = ResolveMeshFaceStyle(in sample, origin, orientationByte, shape, in cachedBrick, BrickFace.Front, fullyCulledFaces, out Vector3 frontTint, ref hasAnyTint);
        int backIndex = ResolveMeshFaceStyle(in sample, origin, orientationByte, shape, in cachedBrick, BrickFace.Back, fullyCulledFaces, out Vector3 backTint, ref hasAnyTint);
        int leftIndex = ResolveMeshFaceStyle(in sample, origin, orientationByte, shape, in cachedBrick, BrickFace.Left, fullyCulledFaces, out Vector3 leftTint, ref hasAnyTint);
        int rightIndex = ResolveMeshFaceStyle(in sample, origin, orientationByte, shape, in cachedBrick, BrickFace.Right, fullyCulledFaces, out Vector3 rightTint, ref hasAnyTint);

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

    private int ResolveMeshFaceStyle(
        in VoxelSample sample,
        Vector3 origin,
        byte orientationByte,
        BrickShape shape,
        in VoxelSurfaceCache.CachedBrick cachedBrick,
        BrickFace face,
        BrickFaceMask fullyCulledFaces,
        out Vector3 tint,
        ref bool hasAnyTint
    ) {
        if ((fullyCulledFaces & face.ToMask()) != 0)
        {
            tint = Vector3.One;
            return -1;
        }

        ConnectedTextureResolver.FaceSurface surface = _connectedTextureResolver.ResolveFaceSurface(in sample, origin, in cachedBrick, face);
        tint = surface.Tint;
        if (surface.HasTint)
        {
            hasAnyTint = true;
        }

        bool shouldConnect = surface.IsConnected && ConnectedTextureResolver.GetAllowedEdgeMask(shape, face) != 0;
        if (!surface.HasTextureOverride && !surface.HasDeclaredFaceTexture && !shouldConnect)
        {
            return -1;
        }

        return _connectedTextureResolver.ResolveTextureIndex(in sample, orientationByte, in surface, face);
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
    
    private readonly struct CullableMesh
    {
        public readonly Mesh Mesh;
        public readonly BrickFaceMask[]? TriangleFaces;
        public readonly BrickFaceMask BoundaryFaces;
        public readonly BrickFaceMask FullBoundaryFaces;
        public readonly bool HasUniformTriangleNormals;

        public CullableMesh(Mesh mesh)
        {
            Mesh = mesh;
            TriangleFaces = BuildTriangleFaceMasks(mesh, out BoundaryFaces, out FullBoundaryFaces, out HasUniformTriangleNormals);
        }

        public CullableMesh(Mesh mesh, BrickFaceMask[]? triangleFaces)
        {
            Mesh = mesh;
            TriangleFaces = triangleFaces;
            BoundaryFaces = BrickFaceMask.None;
            FullBoundaryFaces = BrickFaceMask.None;
            HasUniformTriangleNormals = false;
        }

        private static BrickFaceMask[]? BuildTriangleFaceMasks(
            Mesh? mesh,
            out BrickFaceMask boundaryFaces,
            out BrickFaceMask fullBoundaryFaces,
            out bool hasUniformTriangleNormals
        ) {
            boundaryFaces = BrickFaceMask.None;
            fullBoundaryFaces = BrickFaceMask.None;
            hasUniformTriangleNormals = true;

            if (mesh == null)
            {
                hasUniformTriangleNormals = false;
                return null;
            }

            BrickFaceMask interiorFaces = BrickFaceMask.None;
            int triangleCount = mesh.Triangles.Length / 3;
            var masks = new BrickFaceMask[triangleCount];
            for (var t = 0; t < triangleCount; t++)
            {
                int triOffset = t * 3;
                int i0 = (int)mesh.Triangles[triOffset + 0];
                int i1 = (int)mesh.Triangles[triOffset + 1];
                int i2 = (int)mesh.Triangles[triOffset + 2];
                if (i0 >= mesh.Vertices.Length || i1 >= mesh.Vertices.Length || i2 >= mesh.Vertices.Length ||
                    i0 >= mesh.Normals.Length || i1 >= mesh.Normals.Length || i2 >= mesh.Normals.Length)
                {
                    hasUniformTriangleNormals = false;
                    continue;
                }

                Vector3 n0 = mesh.Normals[i0];
                if (n0 != mesh.Normals[i1] || n0 != mesh.Normals[i2])
                {
                    hasUniformTriangleNormals = false;
                }

                BrickFaceMask boundaryFace = ClassifyBoundaryTriangle(
                    mesh.Vertices[i0],
                    mesh.Vertices[i1],
                    mesh.Vertices[i2],
                    n0
                );
                masks[t] = boundaryFace;
                if (boundaryFace != BrickFaceMask.None)
                {
                    boundaryFaces |= boundaryFace;
                }
                else
                {
                    interiorFaces |= BrickFaceExtensions.FromLocalNormal(n0).ToMask();
                }
            }

            fullBoundaryFaces = boundaryFaces & ~interiorFaces;
            return masks;
        }

        private static BrickFaceMask ClassifyBoundaryTriangle(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 normal)
        {
            const float half = 0.5f - BOUNDARY_EPSILON;
            const float normalThreshold = 1f - NORMAL_EPSILON;

            if (normal.Y > normalThreshold && v0.Y >= half && v1.Y >= half && v2.Y >= half)
            {
                return BrickFaceMask.Top;
            }

            if (normal.Y < -normalThreshold && v0.Y <= -half && v1.Y <= -half && v2.Y <= -half)
            {
                return BrickFaceMask.Bottom;
            }

            if (normal.Z > normalThreshold && v0.Z >= half && v1.Z >= half && v2.Z >= half)
            {
                return BrickFaceMask.Front;
            }

            if (normal.Z < -normalThreshold && v0.Z <= -half && v1.Z <= -half && v2.Z <= -half)
            {
                return BrickFaceMask.Back;
            }

            if (normal.X < -normalThreshold && v0.X <= -half && v1.X <= -half && v2.X <= -half)
            {
                return BrickFaceMask.Left;
            }

            if (normal.X > normalThreshold && v0.X >= half && v1.X >= half && v2.X >= half)
            {
                return BrickFaceMask.Right;
            }

            return BrickFaceMask.None;
        }
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
