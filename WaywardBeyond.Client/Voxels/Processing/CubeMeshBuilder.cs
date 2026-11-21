using System;
using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Voxels;
using WaywardBeyond.Data;
using FaceVertices = (System.Numerics.Vector3 V0, System.Numerics.Vector3 V1, System.Numerics.Vector3 V2, System.Numerics.Vector3 V3);
using FaceUVs = (System.Numerics.Vector2 U0, System.Numerics.Vector2 U1, System.Numerics.Vector2 U2, System.Numerics.Vector2 U3);

namespace WaywardBeyond.Client.Voxels.Processing;

using FaceInfo = (BrickFace Face, Vector3 Normal, FaceVertices Vertices, FaceUVs UV);

internal readonly struct CubeMeshBuilder
{
    private const int NEIGHBOR_LEFT = 0;
    private const int NEIGHBOR_RIGHT = 1;
    private const int NEIGHBOR_AHEAD = 2;
    private const int NEIGHBOR_BEHIND = 3;
    private const int NEIGHBOR_ABOVE = 4;
    private const int NEIGHBOR_BELOW = 5;

    private static readonly FaceInfo _topFace = new(
        BrickFace.Top,
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
        BrickFace.Bottom,
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
        BrickFace.Front,
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
        BrickFace.Back,
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
        BrickFace.Right,
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
        BrickFace.Left,
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
    private readonly IBrickDatabase _brickDatabase;
    private readonly IPaintDatabase? _paintDatabase;
    
    public CubeMeshBuilder(
        TextureArray textureArray,
        MeshState.MeshData meshData,
        IBrickDatabase brickDatabase,
        IPaintDatabase? paintDatabase = null
    ) {
        _textureArray = textureArray;
        _meshData = meshData;
        _brickDatabase = brickDatabase;
        _paintDatabase = paintDatabase;
    }

    public void AddTopFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, Brick brickInfo)
    {
        AddFace(sample, neighbor: ref sample.Above, _topFace, origin, orientation, brickInfo);
    }
    
    public void AddBottomFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, Brick brickInfo)
    {
        AddFace(sample, neighbor: ref sample.Below, _bottomFace, origin, orientation, brickInfo);
    }
    
    public void AddFrontFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, Brick brickInfo)
    {
        AddFace(sample, neighbor: ref sample.Ahead, _frontFace, origin, orientation, brickInfo);
    }
    
    public void AddBackFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, Brick brickInfo)
    {
        AddFace(sample, neighbor: ref sample.Behind, _backFace, origin, orientation, brickInfo);
    }
    
    public void AddRightFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, Brick brickInfo)
    {
        AddFace(sample, neighbor: ref sample.Right, _rightFace, origin, orientation, brickInfo);
    }
    
    public void AddLeftFace(Vector3 origin, Quaternion orientation, in VoxelSample sample, Brick brickInfo)
    {
        AddFace(sample, neighbor: ref sample.Left, _leftFace, origin, orientation, brickInfo);
    }
    
    private void AddFace(
        in VoxelSample sample,
        ref Voxel neighbor,
        FaceInfo faceInfo,
        Vector3 origin,
        Quaternion orientation,
        Brick brickInfo
    ) {
        var vertexStart = (uint)_meshData.Vertices.Count;

        ResolveCenterFaceSurface(in sample, origin, brickInfo, faceInfo.Face, out string? textureName, out bool isConnected, out Vector3 tint);

        int textureIndex = GetTextureIndex(
            in sample,
            orientation,
            textureName,
            isConnected,
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
        
        //  VertexColor.rgb carries linear albedo tint; VertexColor.a carries voxel light.
        ShapeLight shapeLight = neighbor.ShapeLight;
        float light = Math.Clamp(shapeLight.LightLevel / 15f, 0.1f, 1f);
        var color = new Vector4(tint.X, tint.Y, tint.Z, light);
        _meshData.Colors.Add(color);
        _meshData.Colors.Add(color);
        _meshData.Colors.Add(color);
        _meshData.Colors.Add(color);
        
        _meshData.Vertices.Add(origin + Vector3.Transform(faceInfo.Vertices.V0, orientation));
        _meshData.Vertices.Add(origin + Vector3.Transform(faceInfo.Vertices.V1, orientation));
        _meshData.Vertices.Add(origin + Vector3.Transform(faceInfo.Vertices.V2, orientation));
        _meshData.Vertices.Add(origin + Vector3.Transform(faceInfo.Vertices.V3, orientation));
    }

    private void ResolveCenterFaceSurface(
        in VoxelSample sample,
        Vector3 origin,
        Brick brickInfo,
        BrickFace face,
        out string? textureName,
        out bool isConnected,
        out Vector3 tint
    ) {
        tint = Vector3.One;
        if (sample.Center.HasFaceOverrides() &&
            sample.VoxelIndex >= 0 &&
            sample.ChunkData.TryGetFaceOverride(sample.VoxelIndex, face, out FaceOverride faceOverride))
        {
            if (faceOverride.ColorPaintId != 0 && _paintDatabase != null)
            {
                Result<Paint> colorResult = _paintDatabase.Get(faceOverride.ColorPaintId);
                if (colorResult.Success && colorResult.Value.Kind == PaintKind.Color)
                {
                    tint = colorResult.Value.LinearColor;
                }
            }

            if (faceOverride.TexturePaintId != 0)
            {
                if (_paintDatabase != null)
                {
                    Result<Paint> textureResult = _paintDatabase.Get(faceOverride.TexturePaintId);
                    if (textureResult.Success && textureResult.Value.Kind == PaintKind.Texture)
                    {
                        textureName = GetTextureName(origin, textureResult.Value.Textures);
                        isConnected = textureResult.Value.Connected;
                        return;
                    }
                }

                //  Unresolved extension paint id falls back to layer 0 (missing checkerboard), non-connected.
                textureName = null;
                isConnected = false;
                return;
            }
        }

        textureName = GetTextureName(origin, GetBrickFaceTextures(brickInfo.Textures, face));
        isConnected = brickInfo.Textures.Connected;
    }

    /// <summary>
    ///     Gets the best matching texture array index for a face, applying 4-bit connected-texture
    ///     adjacency across both native bricks and painted faces when enabled.
    /// </summary>
    private int GetTextureIndex(
        in VoxelSample sample,
        Quaternion centerQuaternion,
        string? textureName,
        bool isConnected,
        BrickFace face
    ) {
        int textureIndex = textureName != null ? Math.Max(_textureArray.IndexOf(textureName), 0) : 0;
        
        if (!isConnected || textureName == null)
        {
            return textureIndex;
        }
        
        ShapeLight shapeLight = sample.Center.ShapeLight;
        Orientation centerOrientation = sample.Center.GetOrientation();
        Vector3 faceWorldNormal = Vector3.Transform(face.ToLocalNormal(), centerQuaternion);
        var connectedTextureMask = 0;
        
        //  Neighbor bit masking is ordered: Up, left, right, down
        switch (face)
        {
            case BrickFace.Top:
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Behind, NEIGHBOR_BEHIND, bit: 1, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Left, NEIGHBOR_LEFT, bit: 2, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Right, NEIGHBOR_RIGHT, bit: 4, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Ahead, NEIGHBOR_AHEAD, bit: 8, ref connectedTextureMask);
                break;
            case BrickFace.Bottom:
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Ahead, NEIGHBOR_AHEAD, bit: 1, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Left, NEIGHBOR_LEFT, bit: 2, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Right, NEIGHBOR_RIGHT, bit: 4, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Behind, NEIGHBOR_BEHIND, bit: 8, ref connectedTextureMask);
                break;
            case BrickFace.Left:
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Above, NEIGHBOR_ABOVE, bit: 1, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Behind, NEIGHBOR_BEHIND, bit: 2, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Ahead, NEIGHBOR_AHEAD, bit: 4, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Below, NEIGHBOR_BELOW, bit: 8, ref connectedTextureMask);
                break;
            case BrickFace.Right:
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Above, NEIGHBOR_ABOVE, bit: 1, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Ahead, NEIGHBOR_AHEAD, bit: 2, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Behind, NEIGHBOR_BEHIND, bit: 4, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Below, NEIGHBOR_BELOW, bit: 8, ref connectedTextureMask);
                break;
            case BrickFace.Front:
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Above, NEIGHBOR_ABOVE, bit: 1, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Left, NEIGHBOR_LEFT, bit: 2, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Right, NEIGHBOR_RIGHT, bit: 4, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Below, NEIGHBOR_BELOW, bit: 8, ref connectedTextureMask);
                break;
            case BrickFace.Back:
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Above, NEIGHBOR_ABOVE, bit: 1, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Right, NEIGHBOR_RIGHT, bit: 2, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Left, NEIGHBOR_LEFT, bit: 4, ref connectedTextureMask);
                ConnectToNeighbor(in sample, centerOrientation, face, faceWorldNormal, textureName, shapeLight, sample.Below, NEIGHBOR_BELOW, bit: 8, ref connectedTextureMask);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(face), face, null);
        }
        
        return textureIndex + connectedTextureMask;
    }
    
    private void ConnectToNeighbor(
        in VoxelSample sample,
        Orientation centerOrientation,
        BrickFace centerFace,
        Vector3 faceWorldNormal,
        string centerTextureName,
        ShapeLight centerShapeLight,
        in Voxel neighbor,
        int neighborDirIndex,
        int bit,
        ref int mask
    ) {
        if (neighbor.ID == 0)
        {
            return;
        }

        ShapeLight neighborShapeLight = neighbor.ShapeLight;
        if (neighborShapeLight.Shape != centerShapeLight.Shape)
        {
            return;
        }

        Orientation neighborOrientation = neighbor.GetOrientation();
        BrickFace neighborFace = neighborOrientation == centerOrientation
            ? centerFace
            : BrickFaceExtensions.FromLocalNormal(Vector3.Transform(faceWorldNormal, Quaternion.Inverse(neighborOrientation.ToQuaternion())));

        Int3 offset = ChunkData.GetRotatedNeighborOffset(centerOrientation, neighborDirIndex);
        int nx = sample.ChunkOffset.X + sample.Coords.X + offset.X;
        int ny = sample.ChunkOffset.Y + sample.Coords.Y + offset.Y;
        int nz = sample.ChunkOffset.Z + sample.Coords.Z + offset.Z;
        var neighborOrigin = new Vector3(nx, ny, nz);

        string? neighborTextureName;
        bool neighborConnected;

        if (neighbor.HasFaceOverrides() &&
            sample.VoxelObject != null &&
            sample.VoxelObject.TryGetFaceOverride(nx, ny, nz, neighborFace, out FaceOverride neighborOverride) &&
            neighborOverride.TexturePaintId != 0)
        {
            if (_paintDatabase == null)
            {
                return;
            }

            Result<Paint> paintResult = _paintDatabase.Get(neighborOverride.TexturePaintId);
            if (!paintResult.Success || paintResult.Value.Kind != PaintKind.Texture)
            {
                return;
            }

            neighborTextureName = GetTextureName(neighborOrigin, paintResult.Value.Textures);
            neighborConnected = paintResult.Value.Connected;
        }
        else
        {
            Result<Brick> neighborBrickResult = _brickDatabase.Get(neighbor.ID);
            if (!neighborBrickResult.Success)
            {
                return;
            }

            Brick neighborBrick = neighborBrickResult.Value;
            neighborTextureName = GetTextureName(neighborOrigin, GetBrickFaceTextures(neighborBrick.Textures, neighborFace));
            neighborConnected = neighborBrick.Textures.Connected;
        }

        if (neighborConnected && string.Equals(neighborTextureName, centerTextureName, StringComparison.Ordinal))
        {
            mask |= bit;
        }
    }

    internal static string?[]? GetBrickFaceTextures(in BrickTextures textures, BrickFace face)
    {
        return face switch
        {
            BrickFace.Top => textures.Top ?? textures.Default,
            BrickFace.Bottom => textures.Bottom ?? textures.Default,
            BrickFace.Front => textures.Front ?? textures.Default,
            BrickFace.Back => textures.Back ?? textures.Default,
            BrickFace.Left => textures.Left ?? textures.Default,
            BrickFace.Right => textures.Right ?? textures.Default,
            _ => textures.Default,
        };
    }

    /// <summary>Selects a deterministic texture name for the voxel origin from a texture variant list.</summary>
    internal static string? GetTextureName(Vector3 origin, string?[]? textures)
    {
        if (textures == null || textures.Length == 0)
        {
            return null;
        }

        if (textures.Length == 1)
        {
            return textures[0];
        }

        int hash = Math.Abs((int)origin.X ^ (int)origin.Y ^ (int)origin.Z);
        return textures[hash % textures.Length];
    }
}
