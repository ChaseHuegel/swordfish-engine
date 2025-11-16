using System;
using System.Numerics;
using Swordfish.Bricks;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Voxels.Models;

using NeighborMask = (int X, int Y, int Z, int Bit);
using FaceVertices = (System.Numerics.Vector3 V0, System.Numerics.Vector3 V1, System.Numerics.Vector3 V2, System.Numerics.Vector3 V3);
using FaceUVs = (System.Numerics.Vector2 U0, System.Numerics.Vector2 U1, System.Numerics.Vector2 U2, System.Numerics.Vector2 U3);

namespace WaywardBeyond.Client.Core.Voxels.Processing;

using FaceInfo = (FaceVertices Vertices, FaceUVs UV);

internal readonly struct CubeMeshBuilder
{
    private static readonly NeighborMask[] _neighborMasksXZ = [
        (0, 0, -1, 1),   // front
        (-1, 0, 0, 2),  // left
        (1, 0, 0, 4),   // right
        (0, 0, 1, 8),  // back
    ];
    
    private static readonly NeighborMask[] _neighborMasksInvXZ = [
        (0, 0, 1, 1),   // front
        (-1, 0, 0, 2),  // left
        (1, 0, 0, 4),   // right
        (0, 0, -1, 8),  // back
    ];

    private static readonly NeighborMask[] _neighborMasksXY = [
        (0, 1, 0, 1),   // up
        (-1, 0, 0, 2),  // left
        (1, 0, 0, 4),   // right
        (0, -1, 0, 8),  // down
    ];
    
    private static readonly NeighborMask[] _neighborMasksInvXY = [
        (0, 1, 0, 1),   // up
        (1, 0, 0, 2),  // left
        (-1, 0, 0, 4),   // right
        (0, -1, 0, 8),  // down
    ];

    private static readonly NeighborMask[] _neighborMasksYZ = [
        (0, 1, 0, 1),   // up
        (0, 0, -1, 2),  // left
        (0, 0, 1, 4),   // right
        (0, -1, 0, 8),  // down
    ];
    
    private static readonly NeighborMask[] _neighborMasksInvYZ = [
        (0, 1, 0, 1),   // up
        (0, 0, 1, 2),  // left
        (0, 0, -1, 4),   // right
        (0, -1, 0, 8),  // down
    ];

    private static readonly FaceInfo _topFace = new(
        new FaceVertices(
            new Vector3(-0.5f, 0.5f,  0.5f),
            new Vector3( 0.5f, 0.5f,  0.5f),
            new Vector3( 0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f)
        ),
        new FaceUVs(
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        )
    );
    
    private static readonly FaceInfo _bottomFace = new(
        new FaceVertices(
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f, -0.5f,  0.5f),
            new Vector3(-0.5f, -0.5f,  0.5f)
        ),
        new FaceUVs(
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        )
    );
    
    private static readonly FaceInfo _frontFace = new(
        new FaceVertices(
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3( 0.5f, -0.5f, 0.5f),
            new Vector3( 0.5f,  0.5f, 0.5f),
            new Vector3(-0.5f,  0.5f, 0.5f)
        ),
        new FaceUVs(
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        )
    );
    
    private static readonly FaceInfo _backFace = new(
        new FaceVertices(
            new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f,  0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f, -0.5f)
        ),
        new FaceUVs(
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        )
    );
    
    private static readonly FaceInfo _rightFace = new(
        new FaceVertices(
            new Vector3(0.5f, -0.5f,  0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f,  0.5f, -0.5f),
            new Vector3(0.5f,  0.5f,  0.5f)
        ),
        new FaceUVs(
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        )
    );
    
    private static readonly FaceInfo _leftFace = new(
        new FaceVertices(
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f, -0.5f)
        ),
        new FaceUVs(
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        )
    );
    
    private readonly TextureArray _textureArray;
    private readonly MeshState _meshState;
    
    public CubeMeshBuilder(
        TextureArray textureArray,
        MeshState meshState
    ) {
        _textureArray = textureArray;
        _meshState = meshState;
    }

    public void AddTopFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(origin, brickInfo.Textures.Top ?? brickInfo.Textures.Default);
        AddFace(
            sample,
            neighbor: ref sample.Above,
            normal: new Vector3(0, 1, 0),
            _topFace,
            origin,
            orientation,
            brickInfo,
            textureName,
            cullingX: 0, cullingY: 1, cullingZ: 0,
            _neighborMasksXZ
        );
    }
    
    public void AddBottomFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(origin, brickInfo.Textures.Bottom ?? brickInfo.Textures.Default);
        AddFace(
            sample,
            neighbor: ref sample.Below,
            normal: new Vector3(0, -1, 0),
            _bottomFace,
            origin,
            orientation,
            brickInfo,
            textureName,
            cullingX: 0, cullingY: -1, cullingZ: 0,
            _neighborMasksInvXZ
        );
    }
    
    public void AddFrontFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(origin, brickInfo.Textures.Front ?? brickInfo.Textures.Default);
        AddFace(
            sample,
            neighbor: ref sample.Ahead,
            normal: new Vector3(0, 0, 1),
            _frontFace,
            origin,
            orientation,
            brickInfo,
            textureName,
            cullingX: 0, cullingY: 0, cullingZ: 1,
            _neighborMasksXY
        );
    }
    
    public void AddBackFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(origin, brickInfo.Textures.Back ?? brickInfo.Textures.Default);
        AddFace(
            sample,
            neighbor: ref sample.Behind,
            normal: new Vector3(0, 0, -1),
            _backFace,
            origin,
            orientation,
            brickInfo,
            textureName,
            cullingX: 0, cullingY: 0, cullingZ: -1,
            _neighborMasksInvXY
        );
    }
    
    public void AddRightFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(origin, brickInfo.Textures.Right ?? brickInfo.Textures.Default);
        AddFace(
            sample,
            neighbor: ref sample.Right,
            normal: new Vector3(1, 0, 0),
            _rightFace,
            origin,
            orientation,
            brickInfo,
            textureName,
            cullingX: 1, cullingY: 0, cullingZ: 0,
            _neighborMasksInvYZ
        );
    }
    
    public void AddLeftFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(origin, brickInfo.Textures.Left ?? brickInfo.Textures.Default);
        AddFace(
            sample,
            neighbor: ref sample.Left,
            normal: new Vector3(-1, 0, 0),
            _leftFace,
            origin,
            orientation,
            brickInfo,
            textureName,
            cullingX: -1, cullingY: 0, cullingZ: 0,
            _neighborMasksYZ
        );
    }
    
    private void AddFace(
        in VoxelSample sample,
        ref Voxel neighbor,
        Vector3 normal,
        FaceInfo faceInfo,
        Vector3 origin,
        Quaternion orientation,
        BrickInfo brickInfo,
        string? textureName
    ) {
        var vertexStart = (uint)_meshState.Vertices.Count;
        int textureIndex = GetTextureIndex(
            sample,
            brickInfo,
            textureName
        );
        
        _meshState.UV.Add(new Vector3(faceInfo.UV.U0.X, faceInfo.UV.U0.Y, textureIndex));
        _meshState.UV.Add(new Vector3(faceInfo.UV.U1.X, faceInfo.UV.U1.Y, textureIndex));
        _meshState.UV.Add(new Vector3(faceInfo.UV.U2.X, faceInfo.UV.U2.Y, textureIndex));
        _meshState.UV.Add(new Vector3(faceInfo.UV.U3.X, faceInfo.UV.U3.Y, textureIndex));
        
        _meshState.Triangles.Add(vertexStart + 0);
        _meshState.Triangles.Add(vertexStart + 1);
        _meshState.Triangles.Add(vertexStart + 2);
        _meshState.Triangles.Add(vertexStart + 0);
        _meshState.Triangles.Add(vertexStart + 2);
        _meshState.Triangles.Add(vertexStart + 3);

        Vector3 vertNormal = Vector3.Transform(normal, orientation);
        _meshState.Normals.Add(vertNormal);
        _meshState.Normals.Add(vertNormal);
        _meshState.Normals.Add(vertNormal);
        _meshState.Normals.Add(vertNormal);
        
        ShapeLight shapeLight = neighbor.ShapeLight;
        float light = Math.Clamp(shapeLight.LightLevel / 15f, 0.1f, 1f);
        var color = new Vector4(light, light, light, 1f);
        _meshState.Colors.Add(color);
        _meshState.Colors.Add(color);
        _meshState.Colors.Add(color);
        _meshState.Colors.Add(color);
        
        _meshState.Vertices.Add(origin + Vector3.Transform(faceInfo.Vertices.V0, orientation));
        _meshState.Vertices.Add(origin + Vector3.Transform(faceInfo.Vertices.V1, orientation));
        _meshState.Vertices.Add(origin + Vector3.Transform(faceInfo.Vertices.V2, orientation));
        _meshState.Vertices.Add(origin + Vector3.Transform(faceInfo.Vertices.V3, orientation));
    }
    
    /// <summary>
    ///     Gets the best matching texture array index provided an optional preferred texture
    ///     name for a brick. This may not return the exact index of the input texture, such
    ///     as when selecting connected or random textures. If no texture, or an invalid texture,
    ///     is provided then this will return an index for the default texture.
    /// </summary>
    private int GetTextureIndex(
        in VoxelSample sample,
        BrickInfo brickInfo,
        string? textureName
    ) {
        //  TODO support randomized textures
        int textureIndex = textureName != null ? Math.Max(_textureArray.IndexOf(textureName), 0) : 0;
        
        if (!brickInfo.Textures.Connected)
        {
            return textureIndex;
        }
        
        ShapeLight shapeLight = sample.Center.ShapeLight;
        var connectedTextureMask = 0;
        
        //  YZ plane (Left face)
        // private static readonly NeighborMask[] _neighborMasksYZ = [
        //     (0, 1, 0, 1),   // up
        //     (0, 0, -1, 2),  // left
        //     (0, 0, 1, 4),   // right
        //     (0, -1, 0, 8),  // down
        // ];
        ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Above, bit: 1, ref connectedTextureMask, shapeLight);
        ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Ahead, bit: 2, ref connectedTextureMask, shapeLight);
        ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Behind, bit: 4, ref connectedTextureMask, shapeLight);
        ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Below, bit: 8, ref connectedTextureMask, shapeLight);
        
        return textureIndex + connectedTextureMask;
    }
    
    private static void ConnectToNeighbor(in Voxel voxel, in Voxel neighbor, int bit, ref int mask, ShapeLight shapeLight)
    {
        if (neighbor.ID != voxel.ID) 
        {
            return;
        }
        
        ShapeLight neighborShapeLight = neighbor.ShapeLight;
        if (neighborShapeLight.Shape != shapeLight.Shape)
        {
            return;
        }
        
        mask |= bit;
        
        //  TODO don't connect to culled neighbors. This likely will be best handled by expanding samples to a 3x3.
        // Voxel culler = _grid.Get(nX + cullingX, nY + cullingY, nZ + cullingZ);
        // if (culler.ID == 0 || !_brickDatabase.IsCuller(culler))
        // {
        //     //  Texture is connected to the neighbor if it isn't culled
        //     connectedTextureMask |= neighborMask.Bit;
        // }
    }
    
    private static string? GetTextureName(Vector3 origin, string?[]? textures)
    {
        if (textures == null || textures.Length == 0)
        {
            return null;
        }

        if (textures.Length == 1)
        {
            return textures[0];
        }

        int hash = (int)origin.X ^ (int)origin.Y ^ (int)origin.Z;
        return textures[hash % textures.Length];
    }
}