using System;
using System.Numerics;
using WaywardBeyond.Voxels;
using WaywardBeyond.Data;
using FaceVertices = (System.Numerics.Vector3 V0, System.Numerics.Vector3 V1, System.Numerics.Vector3 V2, System.Numerics.Vector3 V3);
using FaceUVs = (System.Numerics.Vector2 U0, System.Numerics.Vector2 U1, System.Numerics.Vector2 U2, System.Numerics.Vector2 U3);

namespace WaywardBeyond.Client.Voxels.Processing;

using FaceInfo = (BrickFace Face, Vector3 Normal, FaceVertices Vertices, FaceUVs UV);

internal readonly struct CubeMeshBuilder(
    MeshState.MeshData meshData,
    ConnectedTextureResolver connectedTextureResolver
) {
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

        ConnectedTextureResolver.FaceSurface surface = connectedTextureResolver.ResolveFaceSurface(in sample, origin, in cachedBrick, face);
        int textureIndex = connectedTextureResolver.ResolveTextureIndex(in sample, orientationByte, in surface, face);

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
        var color = new Vector4(surface.Tint.X, surface.Tint.Y, surface.Tint.Z, light);
        meshData.Colors.Add(color);
        meshData.Colors.Add(color);
        meshData.Colors.Add(color);
        meshData.Colors.Add(color);
        
        meshData.Vertices.Add(origin + rotated.V0);
        meshData.Vertices.Add(origin + rotated.V1);
        meshData.Vertices.Add(origin + rotated.V2);
        meshData.Vertices.Add(origin + rotated.V3);
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
