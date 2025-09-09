using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.Bricks;
using Swordfish.Graphics;

using IntPos = (int X, int Y, int Z);
using NeighborMask = (int X, int Y, int Z, int Bit);
using FaceVertices = (System.Numerics.Vector3 V0, System.Numerics.Vector3 V1, System.Numerics.Vector3 V2, System.Numerics.Vector3 V3);

namespace WaywardBeyond.Client.Core.Bricks;

internal struct BrickGridMeshBuilder
{
    private static readonly NeighborMask[] NeighborMasksXZ = new[]
    {
        (0, 0, 1, 1),   // front
        (-1, 0, 0, 2),  // left
        (1, 0, 0, 4),   // right
        (0, 0, -1, 8),  // back
    };

    private static readonly NeighborMask[] NeighborMasksXY = new[]
    {
        (0, 1, 0, 1),   // up
        (-1, 0, 0, 2),  // left
        (1, 0, 0, 4),   // right
        (0, -1, 0, 8),  // down
    };

    private static readonly NeighborMask[] NeighborMasksYZ = new[]
    {
        (0, 1, 0, 1),   // up
        (0, 0, -1, 2),  // left
        (0, 0, 1, 4),   // right
        (0, -1, 0, 8),  // down
    };

    private static readonly FaceVertices TopFaceVertices = new(
        new Vector3(-0.5f, 0.5f, 0.5f),
        new Vector3(0.5f, 0.5f, 0.5f),
        new Vector3(0.5f, 0.5f, -0.5f),
        new Vector3(-0.5f, 0.5f, -0.5f)
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
        string? textureName = brickInfo.Textures.Top ?? brickInfo.Textures.Default;
        AddFace(
            Vector3.UnitY,
            TopFaceVertices,
            new IntPos(x, y, z),
            brick,
            brickInfo,
            textureName,
            new IntPos(0, 1, 0),
            NeighborMasksXZ
        );
    }
    
    private void AddFace(
        Vector3 normal,
        FaceVertices faceVertices,
        IntPos pos,
        Brick brick,
        BrickInfo brickInfo,
        string? textureName,
        IntPos cullingOffset,
        NeighborMask[] neighborMasks
    ) {
        var vertexStart = (uint)_vertices.Count;
        int textureIndex = GetTextureIndex(
            pos,
            brick,
            brickInfo,
            textureName,
            cullingOffset,
            neighborMasks
        );
        
        _uvs.Add(new Vector3(0f, 0f, textureIndex));
        _uvs.Add(new Vector3(1f, 0f, textureIndex));
        _uvs.Add(new Vector3(1f, 1f, textureIndex));
        _uvs.Add(new Vector3(0f, 1f, textureIndex));
        
        _triangles.Add(vertexStart + 0);
        _triangles.Add(vertexStart + 1);
        _triangles.Add(vertexStart + 2);
        _triangles.Add(vertexStart + 0);
        _triangles.Add(vertexStart + 2);
        _triangles.Add(vertexStart + 3);
        
        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);
        
        _colors.Add(Vector4.One);
        _colors.Add(Vector4.One);
        _colors.Add(Vector4.One);
        _colors.Add(Vector4.One);
        
        _vertices.Add(_origin + new Vector3(pos.X, pos.Y, pos.Z) + faceVertices.V0);
        _vertices.Add(_origin + new Vector3(pos.X, pos.Y, pos.Z) + faceVertices.V1);
        _vertices.Add(_origin + new Vector3(pos.X, pos.Y, pos.Z) + faceVertices.V2);
        _vertices.Add(_origin + new Vector3(pos.X, pos.Y, pos.Z) + faceVertices.V3);
    }
    
    /// <summary>
    ///     Gets the best matching texture array index provided an optional preferred texture
    ///     name for a brick. This may not return the exact index of the input texture, such
    ///     as when selecting connected or random textures. If no texture, or an invalid texture,
    ///     is provided then this will return an index for the default texture.
    /// </summary>
    private int GetTextureIndex(
        IntPos pos,
        Brick brick,
        BrickInfo brickInfo,
        string? textureName,
        IntPos cullingOffset,
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
            int x = pos.X + neighborMask.X;
            int y = pos.Y + neighborMask.Y;
            int z = pos.Z + neighborMask.Z;
            
            Brick neighbor = _grid.Get(x, y, z);
            if (neighbor.ID != brick.ID) 
            {
                continue;
            }
            
            Brick culler = _grid.Get(x + cullingOffset.X, y + cullingOffset.Y, z + cullingOffset.Z);
            if (culler.ID == 0 || !_brickDatabase.Get(culler.ID).Value.DoesCull)
            {
                //  Texture is connected to the neighbor if it isn't culled
                connectedTextureMask |= neighborMask.Bit;
            }
        }
        
        return textureIndex + connectedTextureMask;
    }
}