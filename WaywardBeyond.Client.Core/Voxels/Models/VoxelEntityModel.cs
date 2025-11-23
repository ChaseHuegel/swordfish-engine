using System;
using System.Numerics;

namespace WaywardBeyond.Client.Core.Voxels.Models;

internal struct VoxelEntityModel(in Guid guid, in Vector3 position, in Quaternion orientation, in VoxelObject voxelObject)
{
    public Guid Guid = guid;
    public Vector3 Position = position;
    public Quaternion Orientation = orientation;
    public Vector3 Scale = Vector3.One;
    public VoxelObject VoxelObject = voxelObject;
}