using System.Numerics;
using Swordfish.ECS;

namespace WaywardBeyond.Client.Core.Voxels.Models;

internal struct VoxelEntityModel(in Uuid uuid, in Vector3 position, in Quaternion orientation, in VoxelObject voxelObject)
{
    public Uuid Uuid = uuid;
    public Vector3 Position = position;
    public Quaternion Orientation = orientation;
    public Vector3 Scale = Vector3.One;
    public VoxelObject VoxelObject = voxelObject;
}