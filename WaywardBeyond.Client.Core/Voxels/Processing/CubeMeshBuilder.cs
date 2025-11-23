using System;
using System.Numerics;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Voxels.Models;

using FaceVertices = (System.Numerics.Vector3 V0, System.Numerics.Vector3 V1, System.Numerics.Vector3 V2, System.Numerics.Vector3 V3);
using FaceUVs = (System.Numerics.Vector2 U0, System.Numerics.Vector2 U1, System.Numerics.Vector2 U2, System.Numerics.Vector2 U3);

namespace WaywardBeyond.Client.Core.Voxels.Processing;

using FaceInfo = (CubeMeshBuilder.Face Face, Vector3 Normal, FaceVertices Vertices, FaceUVs UV);

internal readonly struct CubeMeshBuilder
{
    private static readonly FaceInfo _topFace = new(
        Face.Top,
        new Vector3(0, 1, 0),
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
        Face.Bottom,
        new Vector3(0, -1, 0),
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
        Face.Front,
        new Vector3(0, 0, 1),
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
        Face.Back,
        new Vector3(0, 0, -1),
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
        Face.Right,
        new Vector3(1, 0, 0),
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
        Face.Left,
        new Vector3(-1, 0, 0),
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
    private readonly MeshState.MeshData _meshData;
    
    public CubeMeshBuilder(
        TextureArray textureArray,
        MeshState.MeshData meshData
    ) {
        _textureArray = textureArray;
        _meshData = meshData;
    }

    public void AddTopFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(origin, brickInfo.Textures.Top ?? brickInfo.Textures.Default);
        AddFace(
            sample,
            neighbor: ref sample.Above,
            _topFace,
            origin,
            orientation,
            brickInfo,
            textureName
        );
    }
    
    public void AddBottomFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(origin, brickInfo.Textures.Bottom ?? brickInfo.Textures.Default);
        AddFace(
            sample,
            neighbor: ref sample.Below,
            _bottomFace,
            origin,
            orientation,
            brickInfo,
            textureName
        );
    }
    
    public void AddFrontFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(origin, brickInfo.Textures.Front ?? brickInfo.Textures.Default);
        AddFace(
            sample,
            neighbor: ref sample.Ahead,
            _frontFace,
            origin,
            orientation,
            brickInfo,
            textureName
        );
    }
    
    public void AddBackFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(origin, brickInfo.Textures.Back ?? brickInfo.Textures.Default);
        AddFace(
            sample,
            neighbor: ref sample.Behind,
            _backFace,
            origin,
            orientation,
            brickInfo,
            textureName
        );
    }
    
    public void AddRightFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(origin, brickInfo.Textures.Right ?? brickInfo.Textures.Default);
        AddFace(
            sample,
            neighbor: ref sample.Right,
            _rightFace,
            origin,
            orientation,
            brickInfo,
            textureName
        );
    }
    
    public void AddLeftFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(origin, brickInfo.Textures.Left ?? brickInfo.Textures.Default);
        AddFace(
            sample,
            neighbor: ref sample.Left,
            _leftFace,
            origin,
            orientation,
            brickInfo,
            textureName
        );
    }
    
    private void AddFace(
        in VoxelSample sample,
        ref Voxel neighbor,
        FaceInfo faceInfo,
        Vector3 origin,
        Quaternion orientation,
        BrickInfo brickInfo,
        string? textureName
    ) {
        var vertexStart = (uint)_meshData.Vertices.Count;
        int textureIndex = GetTextureIndex(
            sample,
            brickInfo,
            textureName,
            faceInfo.Face
        );
        
        _meshData.UV.Add(new Vector3(faceInfo.UV.U0.X, faceInfo.UV.U0.Y, textureIndex));
        _meshData.UV.Add(new Vector3(faceInfo.UV.U1.X, faceInfo.UV.U1.Y, textureIndex));
        _meshData.UV.Add(new Vector3(faceInfo.UV.U2.X, faceInfo.UV.U2.Y, textureIndex));
        _meshData.UV.Add(new Vector3(faceInfo.UV.U3.X, faceInfo.UV.U3.Y, textureIndex));
        
        _meshData.Triangles.Add(vertexStart + 0);
        _meshData.Triangles.Add(vertexStart + 1);
        _meshData.Triangles.Add(vertexStart + 2);
        _meshData.Triangles.Add(vertexStart + 0);
        _meshData.Triangles.Add(vertexStart + 2);
        _meshData.Triangles.Add(vertexStart + 3);

        Vector3 vertNormal = Vector3.Transform(faceInfo.Normal, orientation);
        _meshData.Normals.Add(vertNormal);
        _meshData.Normals.Add(vertNormal);
        _meshData.Normals.Add(vertNormal);
        _meshData.Normals.Add(vertNormal);
        
        //  TODO #299 Rotated blocks receive lighting on the wrong faces
        //            because samples don't account for orientation
        ShapeLight shapeLight = neighbor.ShapeLight;
        float light = Math.Clamp(shapeLight.LightLevel / 15f, 0.1f, 1f);
        var color = new Vector4(light, light, light, 1f);
        _meshData.Colors.Add(color);
        _meshData.Colors.Add(color);
        _meshData.Colors.Add(color);
        _meshData.Colors.Add(color);
        
        _meshData.Vertices.Add(origin + Vector3.Transform(faceInfo.Vertices.V0, orientation));
        _meshData.Vertices.Add(origin + Vector3.Transform(faceInfo.Vertices.V1, orientation));
        _meshData.Vertices.Add(origin + Vector3.Transform(faceInfo.Vertices.V2, orientation));
        _meshData.Vertices.Add(origin + Vector3.Transform(faceInfo.Vertices.V3, orientation));
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
        string? textureName,
        Face face
    ) {
        //  TODO support randomized textures
        int textureIndex = textureName != null ? Math.Max(_textureArray.IndexOf(textureName), 0) : 0;
        
        if (!brickInfo.Textures.Connected)
        {
            return textureIndex;
        }
        
        ShapeLight shapeLight = sample.Center.ShapeLight;
        var connectedTextureMask = 0;
        
        //  Neighbor bit masking is ordered: Up, left, right, down
        switch (face)
        {
            case Face.Top:
                //  XZ plane (Top face)
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Behind, bit: 1, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Left, bit: 2, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Right, bit: 4, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Ahead, bit: 8, ref connectedTextureMask, shapeLight);
                break;
            case Face.Bottom:
                //  Inv XZ plane (Bottom face)
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Ahead, bit: 1, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Left, bit: 2, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Right, bit: 4, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Behind, bit: 8, ref connectedTextureMask, shapeLight);
                break;
            case Face.Left:
                //  YZ plane (Left face)
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Above, bit: 1, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Behind, bit: 2, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Ahead, bit: 4, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Below, bit: 8, ref connectedTextureMask, shapeLight);
                break;
            case Face.Right:
                //  Inv YZ plane (Right face)
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Above, bit: 1, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Ahead, bit: 2, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Behind, bit: 4, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Below, bit: 8, ref connectedTextureMask, shapeLight);
                break;
            case Face.Front:
                //  XY plane (Front face)
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Above, bit: 1, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Left, bit: 2, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Right, bit: 4, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Below, bit: 8, ref connectedTextureMask, shapeLight);
                break;
            case Face.Back:
                //  Inv XY plane (Back face)
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Above, bit: 1, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Right, bit: 2, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Left, bit: 4, ref connectedTextureMask, shapeLight);
                ConnectToNeighbor(voxel: sample.Center, neighbor: sample.Below, bit: 8, ref connectedTextureMask, shapeLight);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(face), face, null);
        }
        
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
        
        //  TODO #298 don't connect to culled neighbors. This likely will be best handled by expanding samples to a 3x3.
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

    internal enum Face
    {
        Top,
        Bottom,
        Left,
        Right,
        Front,
        Back,
    }
}