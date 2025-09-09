using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.Bricks;
using Swordfish.Graphics;
using Swordfish.Library.Util;

using IntPos = (int X, int Y, int Z);
using NeighborMask = (int X, int Y, int Z, int Bit);

namespace WaywardBeyond.Client.Core.Bricks;

internal sealed class BrickGridBuilder
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
    
    private readonly Mesh _cube;
    private readonly Mesh _slope;
    private readonly TextureArray _textureArray;
    private readonly BrickDatabase _brickDatabase;
    
    public BrickGridBuilder(BrickDatabase brickDatabase, TextureArray textureArray)
    {
        _brickDatabase = brickDatabase;
        _textureArray = textureArray;
        _cube = new Cube();
        _slope = new Slope();
    }
    
    /// <summary>
    ///     Gets the best matching texture array index provided an optional preferred texture
    ///     name for a brick. This may not return the exact index of the input texture, such
    ///     as when selecting connected or random textures. If no texture, or an invalid texture,
    ///     is provided then this will return an index for the default texture.
    /// </summary>
    private int GetTextureIndex(BrickGrid grid, IntPos pos, Brick brick, BrickInfo brickInfo, string? textureName, IntPos cullingOffset, NeighborMask[] neighborMasks)
    {
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
            
            Brick neighbor = grid.Get(x, y, z);
            if (neighbor.ID != brick.ID) 
            {
                continue;
            }
            
            Brick culler = grid.Get(x + cullingOffset.X, y + cullingOffset.Y, z + cullingOffset.Z);
            if (culler.ID == 0 || !_brickDatabase.Get(culler.ID).Value.DoesCull)
            {
                connectedTextureMask |= neighborMask.Bit;
            }
        }
        
        return textureIndex + connectedTextureMask;
    }
    
    public Mesh CreateMesh(BrickGrid grid, bool transparent = false)
    {
        var triangles = new List<uint>();
        var vertices = new List<Vector3>();
        var colors = new List<Vector4>();
        var uv = new List<Vector3>();
        var normals = new List<Vector3>();

        HashSet<BrickGrid> builtGrids = [];
        BuildBrickGridMesh(grid, Vector3.Zero);

        void BuildBrickGridMesh(BrickGrid gridToBuild, Vector3 offset = default)
        {
            if (!builtGrids.Add(gridToBuild))
            {
                return;
            }

            for (var nX = 0; nX < gridToBuild.NeighborGrids.GetLength(0); nX++)
            for (var nY = 0; nY < gridToBuild.NeighborGrids.GetLength(1); nY++)
            for (var nZ = 0; nZ < gridToBuild.NeighborGrids.GetLength(2); nZ++)
            {
                BrickGrid? neighbor = gridToBuild.NeighborGrids[nX, nY, nZ];
                if (neighbor == null)
                {
                    continue;
                }
                
                BuildBrickGridMesh(neighbor, new Vector3(nX - 1, nY - 1, nZ - 1) * gridToBuild.DimensionSize + offset);
            }

            for (var x = 0; x < gridToBuild.DimensionSize; x++)
            for (var y = 0; y < gridToBuild.DimensionSize; y++)
            for (var z = 0; z < gridToBuild.DimensionSize; z++)
            {
                Brick brick = gridToBuild.Bricks[x, y, z];
                
                Brick right = gridToBuild.Get(x + 1, y, z);
                Brick left = gridToBuild.Get(x - 1, y, z);
                Brick above = gridToBuild.Get(x, y + 1, z);
                Brick below = gridToBuild.Get(x, y - 1, z);
                Brick ahead = gridToBuild.Get(x, y, z + 1);
                Brick behind = gridToBuild.Get(x, y, z - 1);
                bool hasRight = right.ID != 0;
                bool hasLeft = left.ID != 0;
                bool hasAbove = above.ID != 0;
                bool hasBelow = below.ID != 0;
                bool hasAhead = ahead.ID != 0;
                bool hasBehind = behind.ID != 0;
                
                if (brick.ID == 0 || (hasRight && hasLeft && hasAbove && hasBelow && hasAhead && hasBehind))
                {
                    continue;
                }
                
                Result<BrickInfo> brickInfoResult = _brickDatabase.Get(brick.ID);
                if (!brickInfoResult.Success)
                {
                    continue;
                }
                
                BrickInfo brickInfo = brickInfoResult.Value;
                
                if (transparent && !brickInfo.Transparent)
                {
                    continue;
                }
                
                if (!transparent && brickInfo.Transparent)
                {
                    continue;
                }
                
                switch (brickInfo.Shape)
                {
                    case BrickShape.Slope:
                        AddMesh(_slope);
                        break;
                    case BrickShape.Custom:
                        AddMesh(brickInfo.Mesh ?? _cube);
                        break;
                    case BrickShape.Block:
                    default:
                        AddCube();
                        break;
                }
                
                continue;
                
                void AddCube()
                {
                    if (!hasAbove)
                    {
                        AddTopFace();
                    }
                    
                    if (!hasBelow)
                    {
                        AddBottomFace();
                    }
                    
                    if (!hasAhead)
                    {
                        AddFrontFace();
                    }
                    
                    if (!hasBehind)
                    {
                        AddBackFace();
                    }
                    
                    if (!hasRight)
                    {
                        AddRightFace();
                    }
                    
                    if (!hasLeft)
                    {
                        AddLeftFace();
                    }
                }
                
                void AddTopFace()
                {
                    string? textureName = brickInfo.Textures.Top ?? brickInfo.Textures.Default;
                    int textureIndex = GetTextureIndex(
                        gridToBuild,
                        new IntPos(x, y, z),
                        brick,
                        brickInfo,
                        textureName,
                        new IntPos(0, 1, 0),
                        NeighborMasksXZ
                    );
                    
                    uv.Add(new Vector3(0f, 0f, textureIndex));
                    uv.Add(new Vector3(1f, 0f, textureIndex));
                    uv.Add(new Vector3(1f, 1f, textureIndex));
                    uv.Add(new Vector3(0f, 1f, textureIndex));

                    var vertexStart = (uint)vertices.Count;
                    triangles.Add(0 + vertexStart);
                    triangles.Add(1 + vertexStart);
                    triangles.Add(2 + vertexStart);
                    triangles.Add(0 + vertexStart);
                    triangles.Add(2 + vertexStart);
                    triangles.Add(3 + vertexStart);
                    
                    normals.Add(new Vector3(0f, 1f, 0f));
                    normals.Add(new Vector3(0f, 1f, 0f));
                    normals.Add(new Vector3(0f, 1f, 0f));
                    normals.Add(new Vector3(0f, 1f, 0f));
                    
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(-0.5f, 0.5f, 0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(0.5f, 0.5f, 0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(0.5f, 0.5f, -0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(-0.5f, 0.5f, -0.5f));
                    
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);
                }
                
                void AddBottomFace()
                {
                    string? textureName = brickInfo.Textures.Bottom ?? brickInfo.Textures.Default;
                    int textureIndex = GetTextureIndex(
                        gridToBuild,
                        new IntPos(x, y, z),
                        brick,
                        brickInfo,
                        textureName,
                        new IntPos(0, -1, 0),
                        NeighborMasksXZ
                    );

                    uv.Add(new Vector3(0f, 0f, textureIndex));
                    uv.Add(new Vector3(1f, 0f, textureIndex));
                    uv.Add(new Vector3(1f, 1f, textureIndex));
                    uv.Add(new Vector3(0f, 1f, textureIndex));

                    var vertexStart = (uint)vertices.Count;
                    triangles.Add(3 + vertexStart);
                    triangles.Add(2 + vertexStart);
                    triangles.Add(0 + vertexStart);
                    triangles.Add(2 + vertexStart);
                    triangles.Add(1 + vertexStart);
                    triangles.Add(0 + vertexStart);
                    
                    normals.Add(new Vector3(0f, -1f, 0f));
                    normals.Add(new Vector3(0f, -1f, 0f));
                    normals.Add(new Vector3(0f, -1f, 0f));
                    normals.Add(new Vector3(0f, -1f, 0f));
                    
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(-0.5f, -0.5f, 0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(0.5f, -0.5f, 0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(0.5f, -0.5f, -0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(-0.5f, -0.5f, -0.5f));
                    
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);
                }
                
                void AddBackFace()
                {
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);

                    string? textureName = brickInfo.Textures.Back ?? brickInfo.Textures.Default;
                    int textureIndex = textureName != null ? _textureArray.IndexOf(textureName) : 0;
                    uv.Add(new Vector3(0f, 0f, textureIndex));
                    uv.Add(new Vector3(1f, 0f, textureIndex));
                    uv.Add(new Vector3(1f, 1f, textureIndex));
                    uv.Add(new Vector3(0f, 1f, textureIndex));

                    var vertexStart = (uint)vertices.Count;
                    triangles.Add(0 + vertexStart);
                    triangles.Add(1 + vertexStart);
                    triangles.Add(2 + vertexStart);
                    triangles.Add(0 + vertexStart);
                    triangles.Add(2 + vertexStart);
                    triangles.Add(3 + vertexStart);
                    
                    normals.Add(new Vector3(0f, 0f, -1f));
                    normals.Add(new Vector3(0f, 0f, -1f));
                    normals.Add(new Vector3(0f, 0f, -1f));
                    normals.Add(new Vector3(0f, 0f, -1f));
                    
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(-0.5f, 0.5f, -0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(0.5f, 0.5f, -0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(0.5f, -0.5f, -0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(-0.5f, -0.5f, -0.5f));
                }
                
                void AddFrontFace()
                {
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);

                    string? textureName = brickInfo.Textures.Front ?? brickInfo.Textures.Default;
                    int textureIndex = textureName != null ? _textureArray.IndexOf(textureName) : 0;
                    uv.Add(new Vector3(0f, 0f, textureIndex));
                    uv.Add(new Vector3(1f, 0f, textureIndex));
                    uv.Add(new Vector3(1f, 1f, textureIndex));
                    uv.Add(new Vector3(0f, 1f, textureIndex));

                    var vertexStart = (uint)vertices.Count;
                    triangles.Add(3 + vertexStart);
                    triangles.Add(2 + vertexStart);
                    triangles.Add(0 + vertexStart);
                    triangles.Add(2 + vertexStart);
                    triangles.Add(1 + vertexStart);
                    triangles.Add(0 + vertexStart);
                    
                    normals.Add(new Vector3(0f, 0f, 1f));
                    normals.Add(new Vector3(0f, 0f, 1f));
                    normals.Add(new Vector3(0f, 0f, 1f));
                    normals.Add(new Vector3(0f, 0f, 1f));
                    
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(-0.5f, 0.5f, 0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(0.5f, 0.5f, 0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(0.5f, -0.5f, 0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(-0.5f, -0.5f, 0.5f));
                }
                
                void AddRightFace()
                {
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);

                    string? textureName = brickInfo.Textures.Right ?? brickInfo.Textures.Default;
                    int textureIndex = textureName != null ? _textureArray.IndexOf(textureName) : 0;
                    uv.Add(new Vector3(1f, 0f, textureIndex));
                    uv.Add(new Vector3(0f, 0f, textureIndex));
                    uv.Add(new Vector3(0f, 1f, textureIndex));
                    uv.Add(new Vector3(1f, 1f, textureIndex));

                    var vertexStart = (uint)vertices.Count;
                    triangles.Add(0 + vertexStart);
                    triangles.Add(1 + vertexStart);
                    triangles.Add(2 + vertexStart);
                    triangles.Add(0 + vertexStart);
                    triangles.Add(2 + vertexStart);
                    triangles.Add(3 + vertexStart);
                    
                    normals.Add(new Vector3(1f, 0f, 0f));
                    normals.Add(new Vector3(1f, 0f, 0f));
                    normals.Add(new Vector3(1f, 0f, 0f));
                    normals.Add(new Vector3(1f, 0f, 0f));
                    
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(0.5f, 0.5f, -0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(0.5f, 0.5f, 0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(0.5f, -0.5f, 0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(0.5f, -0.5f, -0.5f));
                }
                
                void AddLeftFace()
                {
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);
                    colors.Add(Vector4.One);

                    string? textureName = brickInfo.Textures.Left ?? brickInfo.Textures.Default;
                    int textureIndex = textureName != null ? _textureArray.IndexOf(textureName) : 0;
                    uv.Add(new Vector3(0f, 0f, textureIndex));
                    uv.Add(new Vector3(1f, 0f, textureIndex));
                    uv.Add(new Vector3(1f, 1f, textureIndex));
                    uv.Add(new Vector3(0f, 1f, textureIndex));

                    var vertexStart = (uint)vertices.Count;
                    triangles.Add(3 + vertexStart);
                    triangles.Add(2 + vertexStart);
                    triangles.Add(0 + vertexStart);
                    triangles.Add(2 + vertexStart);
                    triangles.Add(1 + vertexStart);
                    triangles.Add(0 + vertexStart);
                    
                    normals.Add(new Vector3(-1f, 0f, 0f));
                    normals.Add(new Vector3(-1f, 0f, 0f));
                    normals.Add(new Vector3(-1f, 0f, 0f));
                    normals.Add(new Vector3(-1f, 0f, 0f));
                    
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(-0.5f, 0.5f, -0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(-0.5f, 0.5f, 0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(-0.5f, -0.5f, 0.5f));
                    vertices.Add(offset + new Vector3(x, y, z) + new Vector3(-0.5f, -0.5f, -0.5f));
                }
                
                void AddMesh(Mesh mesh)
                {
                    colors.AddRange(mesh.Colors);

                    foreach (Vector3 texCoord in mesh.Uv)
                    {
                        int textureIndex = _textureArray.IndexOf(brickInfo.Textures.Default!);
                        uv.Add(texCoord with { Z = textureIndex >= 0 ? textureIndex : 0 });
                    }

                    foreach (uint tri in mesh.Triangles)
                    {
                        triangles.Add(tri + (uint)vertices.Count);
                    }

                    foreach (Vector3 normal in mesh.Normals)
                    {
                        normals.Add(mesh != _cube ? Vector3.Transform(normal, brick.GetQuaternion()) : normal);
                    }

                    foreach (Vector3 vertex in mesh.Vertices)
                    {
                        vertices.Add((mesh != _cube ? Vector3.Transform(vertex, brick.GetQuaternion()) : vertex) + new Vector3(x, y, z) + offset);
                    }
                }
            }
        }

        var mesh = new Mesh(triangles.ToArray(), vertices.ToArray(), colors.ToArray(), uv.ToArray(), normals.ToArray());
        return mesh;
    }
    
    public Vector3[] CreateCollisionData(BrickGrid grid)
    {
        var empty = new Brick(0);
        
        var points = new List<Vector3>();
        HashSet<BrickGrid> builtGrids = [];
        BuildBrickGridPoints(grid, Vector3.Zero);

        void BuildBrickGridPoints(BrickGrid gridToBuild, Vector3 offset = default)
        {
            if (!builtGrids.Add(gridToBuild))
            {
                return;
            }

            for (var nX = 0; nX < gridToBuild.NeighborGrids.GetLength(0); nX++)
            for (var nY = 0; nY < gridToBuild.NeighborGrids.GetLength(1); nY++)
            for (var nZ = 0; nZ < gridToBuild.NeighborGrids.GetLength(2); nZ++)
            {
                BrickGrid? neighbor = gridToBuild.NeighborGrids[nX, nY, nZ];
                if (neighbor == null)
                {
                    continue;
                }
                
                BuildBrickGridPoints(neighbor, new Vector3(nX - 1, nY - 1, nZ - 1) * gridToBuild.DimensionSize + offset);
            }

            for (var x = 0; x < gridToBuild.DimensionSize; x++)
            for (var y = 0; y < gridToBuild.DimensionSize; y++)
            for (var z = 0; z < gridToBuild.DimensionSize; z++)
            {
                Brick brick = gridToBuild.Bricks[x, y, z];
                if (!brick.Equals(empty) &&
                    (gridToBuild.Get(x + 1, y, z).Equals(empty)
                    || gridToBuild.Get(x - 1, y, z).Equals(empty)
                    || gridToBuild.Get(x, y + 1, z).Equals(empty)
                    || gridToBuild.Get(x, y - 1, z).Equals(empty)
                    || gridToBuild.Get(x, y, z + 1).Equals(empty)
                    || gridToBuild.Get(x, y, z - 1).Equals(empty))
                )
                {
                    points.Add(new Vector3(x, y, z) + offset);
                }
            }
        }

        return [.. points];
    }
}