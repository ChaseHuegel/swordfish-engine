using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.Bricks;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.Bricks;

internal sealed class BrickGridBuilder
{
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

            var meshBuilder = new BrickGridMeshBuilder(
                _brickDatabase,
                _textureArray,
                vertices,
                colors,
                uv,
                triangles,
                normals,
                offset,
                gridToBuild
            );

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
                        meshBuilder.AddTopFace(x, y, z, brick, brickInfo);
                    }
                    
                    if (!hasBelow)
                    {
                        meshBuilder.AddBottomFace(x, y, z, brick, brickInfo);
                    }
                    
                    if (!hasAhead)
                    {
                        meshBuilder.AddFrontFace(x, y, z, brick, brickInfo);
                    }
                    
                    if (!hasBehind)
                    {
                        meshBuilder.AddBackFace(x, y, z, brick, brickInfo);
                    }
                    
                    if (!hasRight)
                    {
                        meshBuilder.AddRightFace(x, y, z, brick, brickInfo);
                    }
                    
                    if (!hasLeft)
                    {
                        meshBuilder.AddLeftFace(x, y, z, brick, brickInfo);
                    }
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