using System.Collections.Generic;
using System.Numerics;
using Swordfish.Bricks;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;

namespace WaywardBeyond.Client.Core.Bricks;

internal sealed class BrickGridBuilder
{
    private readonly Mesh _cube;
    private readonly Mesh _slope;
    private readonly Mesh _thruster;
    private readonly Mesh _thrusterBlock;
    private readonly TextureArray _textureArray;
    
    public BrickGridBuilder(IFileParseService fileParseService, TextureArray textureArray)
    {
        _textureArray = textureArray;
        
        _cube = new Cube();
        _slope = new Slope();
        _thruster = fileParseService.Parse<Mesh>(AssetPaths.Models.At("thruster_rocket.obj"));
        _thrusterBlock = fileParseService.Parse<Mesh>(AssetPaths.Models.At("thruster_rocket_internal.obj"));
    }
    
    public Mesh CreateMesh(BrickGrid grid, bool transparent = false)
    {
        var empty = new Brick(0);
        int metalPanelTex = _textureArray.IndexOf("metal_panel");

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
                if (brick.Equals(empty) ||
                    (!gridToBuild.Get(x + 1, y, z).Equals(empty)
                     && !gridToBuild.Get(x - 1, y, z).Equals(empty)
                     && !gridToBuild.Get(x, y + 1, z).Equals(empty)
                     && !gridToBuild.Get(x, y - 1, z).Equals(empty)
                     && !gridToBuild.Get(x, y, z + 1).Equals(empty)
                     && !gridToBuild.Get(x, y, z - 1).Equals(empty)))
                {
                    continue;
                }

                if (transparent && !brick.Name.Contains("window") && !brick.Name.Contains("porthole"))
                {
                    continue;
                }

                if (!transparent && (brick.Name.Contains("window") || brick.Name.Contains("porthole")))
                {
                    continue;
                }

                Mesh brickMesh = MeshFromBrickID(brick.ID);
                colors.AddRange(brickMesh.Colors);

                Mesh MeshFromBrickID(int id)
                {
                    return id switch
                    {
                        2 => _slope,
                        3 => _thruster,
                        4 => _thrusterBlock,
                        _ => _cube,
                    };
                }

                foreach (Vector3 texCoord in brickMesh.Uv)
                {
                    int textureIndex = _textureArray.IndexOf(brick.Name);
                    uv.Add(texCoord with { Z = textureIndex >= 0 ? textureIndex : metalPanelTex });
                }

                foreach (uint tri in brickMesh.Triangles)
                {
                    triangles.Add(tri + (uint)vertices.Count);
                }

                foreach (Vector3 normal in brickMesh.Normals)
                {
                    normals.Add(brickMesh != _cube ? Vector3.Transform(normal, brick.GetQuaternion()) : normal);
                }

                foreach (Vector3 vertex in brickMesh.Vertices)
                {
                    vertices.Add((brickMesh != _cube ? Vector3.Transform(vertex, brick.GetQuaternion()) : vertex) + new Vector3(x, y, z) + offset);
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