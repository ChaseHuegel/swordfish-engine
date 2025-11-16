using System;
using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Graphics;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class MeshPostPass(
    in MeshState meshState,
    in BrickDatabase brickDatabase,
    in PBRTextureArrays textureArrays,
    in IAssetDatabase<Mesh> meshDatabase,
    in bool transparent)
    : VoxelObjectProcessor.ISamplePass
{
    private readonly MeshState _meshState = meshState;
    private readonly BrickDatabase _brickDatabase = brickDatabase;
    private readonly PBRTextureArrays _textureArrays = textureArrays;

    private readonly CubeMeshBuilder _cubeMeshBuilder = new(brickDatabase, textureArrays.Albedo, meshState);
    private readonly bool _transparent = transparent;
    private readonly Mesh _slope = meshDatabase.Get("slope.obj");
    private readonly Mesh _stair = meshDatabase.Get("stair.obj");
    private readonly Mesh _slab = meshDatabase.Get("slab.obj");
    private readonly Mesh _column = meshDatabase.Get("column.obj");
    private readonly Mesh _plate = meshDatabase.Get("plate.obj");

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
        
        Result<BrickInfo> brickInfoResult = _brickDatabase.Get(sample.Center.ID);
        if (!brickInfoResult.Success)
        {
            return;
        }
        
        BrickInfo brickInfo = brickInfoResult.Value;
        
        if (_transparent && !brickInfo.Transparent)
        {
            return;
        }
        
        if (!_transparent && brickInfo.Transparent)
        {
            return;
        }
        
        bool culledRight = IsCulledBy(target: sample.Center, neighbor: sample.Right);
        bool culledLeft = IsCulledBy(target: sample.Center, neighbor: sample.Left);
        bool culledAbove = IsCulledBy(target: sample.Center, neighbor: sample.Above);
        bool culledBelow = IsCulledBy(target: sample.Center, neighbor: sample.Below);
        bool culledAhead = IsCulledBy(target: sample.Center, neighbor: sample.Ahead);
        bool culledBehind = IsCulledBy(target: sample.Center, neighbor: sample.Behind);
        
        if (culledRight && culledLeft && culledAbove && culledBelow && culledAhead && culledBehind)
        {
            return;
        }
        
        var orientation = ((Orientation)sample.Center.Orientation).ToQuaternion();
        ShapeLight shapeLight = sample.Center.ShapeLight;
        var offset = new Vector3(sample.ChunkOffset.X, sample.ChunkOffset.Y, sample.ChunkOffset.Z);
        
        switch (shapeLight.Shape)
        {
            case BrickShape.Slab:
                AddMesh(sample.Coords, offset, brickInfo, _slab, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Stair:
                AddMesh(sample.Coords, offset, brickInfo, _stair, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Slope:
                AddMesh(sample.Coords, offset, brickInfo, _slope, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Column:
                AddMesh(sample.Coords, offset, brickInfo, _column, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Plate:
                AddMesh(sample.Coords, offset, brickInfo, _plate, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Custom:
                if (brickInfo.Mesh == null)
                {
                    AddCube(sample.Coords, offset, orientation, sample, brickInfo, culledAbove, culledBelow, culledAhead, culledBehind, culledRight, culledLeft);
                    break;
                }
                
                AddMesh(sample.Coords, offset, brickInfo, brickInfo.Mesh, orientation, shapeLight.LightLevel);
                break;
            case BrickShape.Block:
            default:
                AddCube(sample.Coords, offset, orientation, sample, brickInfo, culledAbove, culledBelow, culledAhead, culledBehind, culledRight, culledLeft);
                break;
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
        if (neighbor.ID == target.ID && shapeLight.Shape == BrickShape.Block)
        {
            return true;
        }
        
        return _brickDatabase.IsCuller(neighbor);
    }
    
    private void AddCube(
        Int3 coords,
        Vector3 offset,
        Quaternion orientation,
        in VoxelSample sample,
        BrickInfo brickInfo,
        bool culledAbove,
        bool culledBelow,
        bool culledAhead,
        bool culledBehind,
        bool culledRight,
        bool culledLeft
    ) {
        Vector3 origin = offset + new Vector3(coords.X, coords.Y, coords.Z);
            
        if (!culledAbove)
        {
            _cubeMeshBuilder.AddTopFace(origin, orientation, sample, brickInfo);
        }
        
        if (!culledBelow)
        {
            _cubeMeshBuilder.AddBottomFace(origin, orientation, sample, brickInfo);
        }
        
        if (!culledAhead)
        {
            _cubeMeshBuilder.AddFrontFace(origin, orientation, sample, brickInfo);
        }
        
        if (!culledBehind)
        {
            _cubeMeshBuilder.AddBackFace(origin, orientation, sample, brickInfo);
        }
        
        if (!culledRight)
        {
            _cubeMeshBuilder.AddRightFace(origin, orientation, sample, brickInfo);
        }
        
        if (!culledLeft)
        {
            _cubeMeshBuilder.AddLeftFace(origin, orientation, sample, brickInfo);
        }
    }
    
    private void AddMesh(Int3 coords, Vector3 offset, BrickInfo brickInfo, Mesh mesh, Quaternion orientation, int lightLevel)
    {
        float light = Math.Clamp(lightLevel / 15f, 0.1f, 1f);
        var color = new Vector4(light, light, light, 1f);
        for (var i = 0; i < mesh.Colors.Length; i++)
        {
            _meshState.Colors.Add(color);
        }
        
        foreach (Vector3 texCoord in mesh.Uv)
        {
            int textureIndex = _textureArrays.Albedo.IndexOf(brickInfo.Textures.Default![0]!);
            _meshState.UV.Add(texCoord with { Z = textureIndex >= 0 ? textureIndex : 0 });
        }
        
        foreach (uint tri in mesh.Triangles)
        {
            _meshState.Triangles.Add(tri + (uint)_meshState.Vertices.Count);
        }
        
        foreach (Vector3 normal in mesh.Normals)
        {
            _meshState.Normals.Add(Vector3.Transform(normal, orientation));
        }
        
        foreach (Vector3 vertex in mesh.Vertices)
        {
            _meshState.Vertices.Add(Vector3.Transform(vertex, orientation) + new Vector3(coords.X, coords.Y, coords.Z) + offset);
        }
    }
}