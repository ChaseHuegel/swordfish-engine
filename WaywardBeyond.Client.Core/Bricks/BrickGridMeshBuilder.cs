using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.Bricks;
using Swordfish.Graphics;

using NeighborMask = (int X, int Y, int Z, int Bit);
using FaceVertices = (System.Numerics.Vector3 V0, System.Numerics.Vector3 V1, System.Numerics.Vector3 V2, System.Numerics.Vector3 V3);
using FaceUVs = (System.Numerics.Vector2 U0, System.Numerics.Vector2 U1, System.Numerics.Vector2 U2, System.Numerics.Vector2 U3);

namespace WaywardBeyond.Client.Core.Bricks;

using FaceInfo = (FaceVertices Vertices, FaceUVs UV);

internal readonly struct BrickGridMeshBuilder
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
    
    private readonly BrickDatabase _brickDatabase;
    private readonly TextureArray _textureArray;
    private readonly List<Vector3> _vertices;
    private readonly List<Vector4> _colors;
    private readonly List<Vector3> _uvs;
    private readonly List<uint> _triangles;
    private readonly List<Vector3> _normals;
    private readonly Vector3 _origin;
    private readonly BrickGrid _grid;

    public BrickGridMeshBuilder(
        BrickDatabase brickDatabase,
        TextureArray textureArray,
        List<Vector3> vertices,
        List<Vector4> colors,
        List<Vector3> uvs,
        List<uint> triangles,
        List<Vector3> normals,
        Vector3 origin,
        BrickGrid grid
    ) {
        _brickDatabase = brickDatabase;
        _textureArray = textureArray;
        _vertices = vertices;
        _colors = colors;
        _uvs = uvs;
        _triangles = triangles;
        _normals = normals;
        _origin = origin;
        _grid = grid;
    }

    public void AddTopFace(int x, int y, int z, Brick brick, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(x, y, z, brickInfo.Textures.Top ?? brickInfo.Textures.Default);
        AddFace(
            normal: new Vector3(0, 1, 0),
            _topFace,
            x, y, z,
            brick,
            brickInfo,
            textureName,
            cullingX: 0, cullingY: 1, cullingZ: 0,
            _neighborMasksXZ
        );
    }
    
    public void AddBottomFace(int x, int y, int z, Brick brick, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(x, y, z, brickInfo.Textures.Bottom ?? brickInfo.Textures.Default);
        AddFace(
            normal: new Vector3(0, -1, 0),
            _bottomFace,
            x, y, z,
            brick,
            brickInfo,
            textureName,
            cullingX: 0, cullingY: -1, cullingZ: 0,
            _neighborMasksInvXZ
        );
    }
    
    public void AddFrontFace(int x, int y, int z, Brick brick, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(x, y, z, brickInfo.Textures.Front ?? brickInfo.Textures.Default);
        AddFace(
            normal: new Vector3(0, 0, 1),
            _frontFace,
            x, y, z,
            brick,
            brickInfo,
            textureName,
            cullingX: 0, cullingY: 0, cullingZ: 1,
            _neighborMasksXY
        );
    }
    
    public void AddBackFace(int x, int y, int z, Brick brick, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(x, y, z, brickInfo.Textures.Back ?? brickInfo.Textures.Default);
        AddFace(
            normal: new Vector3(0, 0, -1),
            _backFace,
            x, y, z,
            brick,
            brickInfo,
            textureName,
            cullingX: 0, cullingY: 0, cullingZ: -1,
            _neighborMasksInvXY
        );
    }
    
    public void AddRightFace(int x, int y, int z, Brick brick, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(x, y, z, brickInfo.Textures.Right ?? brickInfo.Textures.Default);
        AddFace(
            normal: new Vector3(1, 0, 0),
            _rightFace,
            x, y, z,
            brick,
            brickInfo,
            textureName,
            cullingX: 1, cullingY: 0, cullingZ: 0,
            _neighborMasksInvYZ
        );
    }
    
    public void AddLeftFace(int x, int y, int z, Brick brick, BrickInfo brickInfo)
    {
        string? textureName = GetTextureName(x, y, z, brickInfo.Textures.Left ?? brickInfo.Textures.Default);
        AddFace(
            normal: new Vector3(-1, 0, 0),
            _leftFace,
            x, y, z,
            brick,
            brickInfo,
            textureName,
            cullingX: -1, cullingY: 0, cullingZ: 0,
            _neighborMasksYZ
        );
    }
    
    private void AddFace(
        Vector3 normal,
        FaceInfo faceInfo,
        int x, int y, int z,
        Brick brick,
        BrickInfo brickInfo,
        string? textureName,
        int cullingX, int cullingY, int cullingZ,
        NeighborMask[] neighborMasks
    ) {
        var vertexStart = (uint)_vertices.Count;
        var rotation = brick.Orientation.ToQuaternion();
        int textureIndex = GetTextureIndex(
            x, y, z,
            brick,
            brickInfo,
            textureName,
            cullingX, cullingY, cullingZ,
            neighborMasks
        );
        
        _uvs.Add(new Vector3(faceInfo.UV.U0.X, faceInfo.UV.U0.Y, textureIndex));
        _uvs.Add(new Vector3(faceInfo.UV.U1.X, faceInfo.UV.U1.Y, textureIndex));
        _uvs.Add(new Vector3(faceInfo.UV.U2.X, faceInfo.UV.U2.Y, textureIndex));
        _uvs.Add(new Vector3(faceInfo.UV.U3.X, faceInfo.UV.U3.Y, textureIndex));
        
        _triangles.Add(vertexStart + 0);
        _triangles.Add(vertexStart + 1);
        _triangles.Add(vertexStart + 2);
        _triangles.Add(vertexStart + 0);
        _triangles.Add(vertexStart + 2);
        _triangles.Add(vertexStart + 3);
        
        _normals.Add(Vector3.Transform(normal, rotation));
        _normals.Add(Vector3.Transform(normal, rotation));
        _normals.Add(Vector3.Transform(normal, rotation));
        _normals.Add(Vector3.Transform(normal, rotation));

        Brick neighbor = _grid.Get(x + (int)normal.X, y + (int)normal.Y, z + (int)normal.Z);
        BrickData data = neighbor.Data;
        float light = Math.Clamp(data.LightLevel / 15f, 0.1f, 1f);
        var color = new Vector4(light, light, light, 1f);
        _colors.Add(color);
        _colors.Add(color);
        _colors.Add(color);
        _colors.Add(color);
        
        _vertices.Add(_origin + new Vector3(x, y, z) + Vector3.Transform(faceInfo.Vertices.V0, rotation));
        _vertices.Add(_origin + new Vector3(x, y, z) + Vector3.Transform(faceInfo.Vertices.V1, rotation));
        _vertices.Add(_origin + new Vector3(x, y, z) + Vector3.Transform(faceInfo.Vertices.V2, rotation));
        _vertices.Add(_origin + new Vector3(x, y, z) + Vector3.Transform(faceInfo.Vertices.V3, rotation));
    }
    
    /// <summary>
    ///     Gets the best matching texture array index provided an optional preferred texture
    ///     name for a brick. This may not return the exact index of the input texture, such
    ///     as when selecting connected or random textures. If no texture, or an invalid texture,
    ///     is provided then this will return an index for the default texture.
    /// </summary>
    private int GetTextureIndex(
        int x, int y, int z,
        Brick brick,
        BrickInfo brickInfo,
        string? textureName,
        int cullingX, int cullingY, int cullingZ,
        NeighborMask[] neighborMasks
    ) {
        int textureIndex = textureName != null ? Math.Max(_textureArray.IndexOf(textureName), 0) : 0;
        
        //  TODO support randomized textures
        if (!brickInfo.Textures.Connected)
        {
            return textureIndex;
        }

        var connectedTextureMask = 0;
        for (var i = 0; i < neighborMasks.Length; i++)
        {
            NeighborMask neighborMask = neighborMasks[i];
            int nX = x + neighborMask.X;
            int nY = y + neighborMask.Y;
            int nZ = z + neighborMask.Z;
            
            Brick neighbor = _grid.Get(nX, nY, nZ);
            
            //  Only connect to neighbors with matching ID and shape.
            BrickData neighborData = neighbor.Data;
            BrickData brickData = brick.Data;
            if (neighbor.ID != brick.ID || neighborData.Shape != brickData.Shape) 
            {
                continue;
            }
            
            Brick culler = _grid.Get(nX + cullingX, nY + cullingY, nZ + cullingZ);
            if (culler.ID == 0 || !_brickDatabase.IsCuller(culler))
            {
                //  Texture is connected to the neighbor if it isn't culled
                connectedTextureMask |= neighborMask.Bit;
            }
        }
        
        return textureIndex + connectedTextureMask;
    }
    
    private static string? GetTextureName(int x, int y, int z, string?[]? textures)
    {
        if (textures == null || textures.Length == 0)
        {
            return null;
        }

        if (textures.Length == 1)
        {
            return textures[0];
        }

        int hash = x ^ y ^ z;
        return textures[hash % textures.Length];
    }
}