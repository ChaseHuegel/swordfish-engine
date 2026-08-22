using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Voxels.Models;

internal struct CharacterEntityModel(
    in Uuid uuid,
    in Vector3 position,
    in Quaternion orientation,
    in GameMode gameMode
) {
    public Uuid Uuid = uuid;
    public Vector3 Position = position;
    public Quaternion Orientation = orientation;
    public Vector3 Scale = Vector3.One;
    public GameMode GameMode = gameMode;
}