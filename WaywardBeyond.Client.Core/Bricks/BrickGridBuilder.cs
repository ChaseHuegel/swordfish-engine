using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.Bricks;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Graphics;

namespace WaywardBeyond.Client.Core.Bricks;

internal sealed class BrickGridBuilder
{
    private readonly Mesh _slope;
    private readonly Mesh _stair;
    private readonly Mesh _slab;
    private readonly Mesh _column;
    private readonly Mesh _plate;
    private readonly PBRTextureArrays _textureArrays;
    private readonly BrickDatabase _brickDatabase;
    
    public BrickGridBuilder(BrickDatabase brickDatabase, PBRTextureArrays textureArrays, IAssetDatabase<Mesh> meshDatabase)
    {
        _brickDatabase = brickDatabase;
        _textureArrays = textureArrays;
        _slope = meshDatabase.Get("slope.obj");
        _stair = meshDatabase.Get("stair.obj");
        _slab = meshDatabase.Get("slab.obj");
        _column = meshDatabase.Get("column.obj");
        _plate = meshDatabase.Get("plate.obj");
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
                _textureArrays.Diffuse,
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
                
                Brick right, left, above, below, ahead, behind;
                if (brick.Orientation.Equals(BrickOrientation.Identity))
                {
                    right = gridToBuild.Get(x + 1, y, z);
                    left = gridToBuild.Get(x - 1, y, z);
                    above = gridToBuild.Get(x, y + 1, z);
                    below = gridToBuild.Get(x, y - 1, z);
                    ahead = gridToBuild.Get(x, y, z + 1);
                    behind = gridToBuild.Get(x, y, z - 1);
                }
                else
                {
                    Quaternion rotation = brick.GetQuaternion();
                
                    Vector3 neighborOffset = Vector3.Transform(new Vector3(1, 0, 0), rotation);
                    right = gridToBuild.Get(x + (int)Math.Round(neighborOffset.X, MidpointRounding.AwayFromZero), y + (int)Math.Round(neighborOffset.Y, MidpointRounding.AwayFromZero), z + (int)Math.Round(neighborOffset.Z, MidpointRounding.AwayFromZero));

                    neighborOffset = Vector3.Transform(new Vector3(-1, 0, 0), rotation);
                    left = gridToBuild.Get(x + (int)Math.Round(neighborOffset.X, MidpointRounding.AwayFromZero), y + (int)Math.Round(neighborOffset.Y, MidpointRounding.AwayFromZero), z + (int)Math.Round(neighborOffset.Z, MidpointRounding.AwayFromZero));
                    
                    neighborOffset = Vector3.Transform(new Vector3(0, 1, 0), rotation);
                    above = gridToBuild.Get(x + (int)Math.Round(neighborOffset.X, MidpointRounding.AwayFromZero), y + (int)Math.Round(neighborOffset.Y, MidpointRounding.AwayFromZero), z + (int)Math.Round(neighborOffset.Z, MidpointRounding.AwayFromZero));
                    
                    neighborOffset = Vector3.Transform(new Vector3(0, -1, 0), rotation);
                    below = gridToBuild.Get(x + (int)Math.Round(neighborOffset.X, MidpointRounding.AwayFromZero), y + (int)Math.Round(neighborOffset.Y, MidpointRounding.AwayFromZero), z + (int)Math.Round(neighborOffset.Z, MidpointRounding.AwayFromZero));
                    
                    neighborOffset = Vector3.Transform(new Vector3(0, 0, 1), rotation);
                    ahead = gridToBuild.Get(x + (int)Math.Round(neighborOffset.X, MidpointRounding.AwayFromZero), y + (int)Math.Round(neighborOffset.Y, MidpointRounding.AwayFromZero), z + (int)Math.Round(neighborOffset.Z, MidpointRounding.AwayFromZero));
                    
                    neighborOffset = Vector3.Transform(new Vector3(0, 0, -1), rotation);
                    behind = gridToBuild.Get(x + (int)Math.Round(neighborOffset.X, MidpointRounding.AwayFromZero), y + (int)Math.Round(neighborOffset.Y, MidpointRounding.AwayFromZero), z + (int)Math.Round(neighborOffset.Z, MidpointRounding.AwayFromZero));
                }
                
                bool hasRight = IsCulledAtNeighbor(target: brick, neighbor: right);
                bool hasLeft = IsCulledAtNeighbor(target: brick, neighbor: left);
                bool hasAbove = IsCulledAtNeighbor(target: brick, neighbor: above);
                bool hasBelow = IsCulledAtNeighbor(target: brick, neighbor: below);
                bool hasAhead = IsCulledAtNeighbor(target: brick, neighbor: ahead);
                bool hasBehind = IsCulledAtNeighbor(target: brick, neighbor: behind);

                bool IsCulledAtNeighbor(Brick target, Brick neighbor)
                {
                    if (neighbor.ID == 0)
                    {
                        return false;
                    }

                    //  Non-block shaped bricks of the same type cull one another 
                    if (neighbor.ID == target.ID && neighbor.Data == 0)
                    {
                        return true;
                    }

                    return _brickDatabase.IsCuller(neighbor);
                }
                
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
                
                switch ((BrickShape)brick.Data)
                {
                    case BrickShape.Slab:
                        AddMesh(_slab);
                        break;
                    case BrickShape.Stair:
                        AddMesh(_stair);
                        break;
                    case BrickShape.Slope:
                        AddMesh(_slope);
                        break;
                    case BrickShape.Column:
                        AddMesh(_column);
                        break;
                    case BrickShape.Plate:
                        AddMesh(_plate);
                        break;
                    case BrickShape.Custom:
                        if (brickInfo.Mesh == null)
                        {
                            AddCube();
                            break;
                        }
                        
                        AddMesh(brickInfo.Mesh);
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
                    Quaternion rotation = brick.GetQuaternion();
                    
                    colors.AddRange(mesh.Colors);

                    foreach (Vector3 texCoord in mesh.Uv)
                    {
                        int textureIndex = _textureArrays.Diffuse.IndexOf(brickInfo.Textures.Default![0]!);
                        uv.Add(texCoord with { Z = textureIndex >= 0 ? textureIndex : 0 });
                    }

                    foreach (uint tri in mesh.Triangles)
                    {
                        triangles.Add(tri + (uint)vertices.Count);
                    }

                    foreach (Vector3 normal in mesh.Normals)
                    {
                        normals.Add(Vector3.Transform(normal, rotation));
                    }

                    foreach (Vector3 vertex in mesh.Vertices)
                    {
                        vertices.Add(Vector3.Transform(vertex, rotation) + new Vector3(x, y, z) + offset);
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