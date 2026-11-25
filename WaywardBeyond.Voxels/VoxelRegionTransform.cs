using System;
using System.Numerics;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;

namespace WaywardBeyond.Voxels;

/// <summary>Applies axis mirroring and 90-degree rotations to <see cref="VoxelRegionData"/>.</summary>
public static class VoxelRegionTransform
{
    /// <summary>Transforms coordinates, voxel orientations, and per-face overrides by <paramref name="mirror"/> then <paramref name="rotation"/>.</summary>
    public static VoxelRegionData Transform(
        in VoxelRegionData source,
        Orientation rotation,
        MirrorAxes mirror,
        IBrickDatabase? brickDatabase = null
    )
    {
        if (source.SizeX <= 0 || source.SizeY <= 0 || source.SizeZ <= 0 || source.Chunks == null)
        {
            return source;
        }

        if (mirror == MirrorAxes.None && rotation == Orientation.Identity)
        {
            return source;
        }

        ComputeTransformedBounds(
            source.SizeX,
            source.SizeY,
            source.SizeZ,
            rotation,
            out Int3 minRotated,
            out Int3 newSize
        );

        byte chunkSize = GetValidChunkSize(source.Chunks);
        var sourceObject = new VoxelObject(chunkSize, source.Chunks);
        var destObject = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        Span<FaceOverride> faces = stackalloc FaceOverride[6];
        Span<FaceOverride> remappedFaces = stackalloc FaceOverride[6];

        for (int sz = 0; sz < source.SizeZ; sz++)
        for (int sy = 0; sy < source.SizeY; sy++)
        for (int sx = 0; sx < source.SizeX; sx++)
        {
            if (!sourceObject.IsResident(sx, sy, sz))
            {
                continue;
            }

            Voxel voxel = sourceObject.Get(sx, sy, sz);
            if (voxel.ID == 0)
            {
                continue;
            }

            faces.Clear();
            bool hasOverrides = voxel.HasFaceOverrides();
            if (hasOverrides)
            {
                for (var f = 0; f < 6; f++)
                {
                    sourceObject.TryGetFaceOverride(sx, sy, sz, (BrickFace)f, out faces[f]);
                }
            }

            BrickShape shape = voxel.GetShapeLight().Shape;
            Orientation voxelOrientation = voxel.GetOrientation();
            bool isOrientable = IsOrientable(brickDatabase, voxel.ID, shape, voxelOrientation);

            int mx = (mirror & MirrorAxes.X) != 0 ? (source.SizeX - 1) - sx : sx;
            int my = (mirror & MirrorAxes.Y) != 0 ? (source.SizeY - 1) - sy : sy;
            int mz = (mirror & MirrorAxes.Z) != 0 ? (source.SizeZ - 1) - sz : sz;

            if (mirror != MirrorAxes.None)
            {
                ApplyMirrorAxis(MirrorAxes.X, mirror, isOrientable, ref voxelOrientation, faces);
                ApplyMirrorAxis(MirrorAxes.Y, mirror, isOrientable, ref voxelOrientation, faces);
                ApplyMirrorAxis(MirrorAxes.Z, mirror, isOrientable, ref voxelOrientation, faces);
            }

            Int3 rotatedCoord = RotateInt3(new Int3(mx, my, mz), rotation);
            int nx = rotatedCoord.X - minRotated.X;
            int ny = rotatedCoord.Y - minRotated.Y;
            int nz = rotatedCoord.Z - minRotated.Z;

            if (rotation != Orientation.Identity)
            {
                if (isOrientable)
                {
                    voxelOrientation = new Orientation(rotation.ToQuaternion() * voxelOrientation.ToQuaternion());
                }
                else
                {
                    voxelOrientation = Orientation.Identity;
                    if (hasOverrides)
                    {
                        remappedFaces.Clear();
                        for (var f = 0; f < 6; f++)
                        {
                            if (!faces[f].IsEmpty)
                            {
                                BrickFace worldFace = FaceOrientationLookup.ToWorldFace(rotation, (BrickFace)f);
                                remappedFaces[(int)worldFace] = faces[f];
                            }
                        }

                        remappedFaces.CopyTo(faces);
                    }
                }
            }

            if (!isOrientable)
            {
                voxelOrientation = Orientation.Identity;
            }

            var transformed = new Voxel(
                voxel.ID,
                new ShapeLight(shape, voxel.GetLightLevel()),
                new OrientationFlags(voxelOrientation, hasFaceOverrides: false)
            );
            destObject.Set(nx, ny, nz, transformed);

            if (hasOverrides)
            {
                for (var f = 0; f < 6; f++)
                {
                    if (!faces[f].IsEmpty)
                    {
                        destObject.SetFaceOverride(nx, ny, nz, ((BrickFace)f).ToMask(), faces[f]);
                    }
                }
            }
        }

        return new VoxelRegionData(
            newSize.X,
            newSize.Y,
            newSize.Z,
            destObject.GetChunkInfos(),
            source.BrickPalette,
            source.PaintPalette
        );
    }

    /// <summary>Computes the axis-aligned dimensions after mirroring and rotating a region of size <paramref name="size"/>.</summary>
    public static Int3 GetTransformedSize(Int3 size, Orientation rotation)
    {
        ComputeTransformedBounds(size.X, size.Y, size.Z, rotation, out _, out Int3 newSize);
        return newSize;
    }

    private static void ComputeTransformedBounds(
        int sizeX,
        int sizeY,
        int sizeZ,
        Orientation rotation,
        out Int3 minRotated,
        out Int3 newSize
    )
    {
        int maxX = Math.Max(0, sizeX - 1);
        int maxY = Math.Max(0, sizeY - 1);
        int maxZ = Math.Max(0, sizeZ - 1);

        int minRX = int.MaxValue;
        int minRY = int.MaxValue;
        int minRZ = int.MaxValue;
        int maxRX = int.MinValue;
        int maxRY = int.MinValue;
        int maxRZ = int.MinValue;

        for (var cx = 0; cx <= 1; cx++)
        for (var cy = 0; cy <= 1; cy++)
        for (var cz = 0; cz <= 1; cz++)
        {
            Int3 corner = RotateInt3(new Int3(cx * maxX, cy * maxY, cz * maxZ), rotation);
            if (corner.X < minRX) minRX = corner.X;
            if (corner.Y < minRY) minRY = corner.Y;
            if (corner.Z < minRZ) minRZ = corner.Z;
            if (corner.X > maxRX) maxRX = corner.X;
            if (corner.Y > maxRY) maxRY = corner.Y;
            if (corner.Z > maxRZ) maxRZ = corner.Z;
        }

        minRotated = new Int3(minRX, minRY, minRZ);
        newSize = new Int3(maxRX - minRX + 1, maxRY - minRY + 1, maxRZ - minRZ + 1);
    }

    private static Int3 RotateInt3(Int3 value, Orientation orientation)
    {
        int x = value.X;
        int y = value.Y;
        int z = value.Z;

        for (var i = 0; i < orientation.PitchRotations; i++)
        {
            (y, z) = (-z, y);
        }

        for (var i = 0; i < orientation.YawRotations; i++)
        {
            (x, z) = (z, -x);
        }

        for (var i = 0; i < orientation.RollRotations; i++)
        {
            (x, y) = (-y, x);
        }

        return new Int3(x, y, z);
    }

    private static void ApplyMirrorAxis(
        MirrorAxes axis,
        MirrorAxes activeMask,
        bool isOrientable,
        ref Orientation orientation,
        Span<FaceOverride> faces
    )
    {
        if ((activeMask & axis) == 0)
        {
            return;
        }

        if (isOrientable)
        {
            orientation = MirrorOrientation(orientation, axis);
            SwapFaces(faces, BrickFace.Left, BrickFace.Right);
            return;
        }

        switch (axis)
        {
            case MirrorAxes.X:
                SwapFaces(faces, BrickFace.Left, BrickFace.Right);
                break;
            case MirrorAxes.Y:
                SwapFaces(faces, BrickFace.Top, BrickFace.Bottom);
                break;
            case MirrorAxes.Z:
                SwapFaces(faces, BrickFace.Front, BrickFace.Back);
                break;
        }
    }

    private static Orientation MirrorOrientation(Orientation orientation, MirrorAxes axis)
    {
        Quaternion q = orientation.ToQuaternion();
        Vector3 ex = -ReflectVector(Vector3.Transform(Vector3.UnitX, q), axis);
        Vector3 ey = ReflectVector(Vector3.Transform(Vector3.UnitY, q), axis);
        Vector3 ez = ReflectVector(Vector3.Transform(Vector3.UnitZ, q), axis);

        var matrix = new Matrix4x4(
            ex.X, ex.Y, ex.Z, 0f,
            ey.X, ey.Y, ey.Z, 0f,
            ez.X, ez.Y, ez.Z, 0f,
            0f, 0f, 0f, 1f
        );
        return new Orientation(Quaternion.CreateFromRotationMatrix(matrix));
    }

    private static Vector3 ReflectVector(Vector3 vector, MirrorAxes axis)
    {
        return axis switch
        {
            MirrorAxes.X => new Vector3(-vector.X, vector.Y, vector.Z),
            MirrorAxes.Y => new Vector3(vector.X, -vector.Y, vector.Z),
            MirrorAxes.Z => new Vector3(vector.X, vector.Y, -vector.Z),
            _ => vector,
        };
    }

    private static void SwapFaces(Span<FaceOverride> faces, BrickFace a, BrickFace b)
    {
        (faces[(int)a], faces[(int)b]) = (faces[(int)b], faces[(int)a]);
    }

    private static bool IsOrientable(IBrickDatabase? brickDatabase, ushort voxelId, BrickShape shape, Orientation orientation)
    {
        if (brickDatabase != null)
        {
            Result<Brick> brickResult = brickDatabase.Get(voxelId);
            if (brickResult.Success)
            {
                return brickResult.Value.IsOrientable(shape);
            }
        }

        return shape != BrickShape.Block || orientation != Orientation.Identity;
    }

    private static byte GetValidChunkSize(ChunkInfo[] chunks)
    {
        byte chunkSize = chunks.Length > 0 ? chunks[0].Chunk.Size : (byte)0;
        if (chunkSize <= 0 || (chunkSize & (chunkSize - 1)) != 0)
        {
            return VoxelObject.DEFAULT_CHUNK_EDGE;
        }

        return chunkSize;
    }
}
