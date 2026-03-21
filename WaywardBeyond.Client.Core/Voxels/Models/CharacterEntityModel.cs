using System;
using System.Numerics;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.Voxels.Models;

internal struct CharacterEntityModel(
    in Guid guid,
    in Vector3 position,
    in Quaternion orientation,
    in GameMode gameMode
) {
    public Guid Guid = guid;
    public Vector3 Position = position;
    public Quaternion Orientation = orientation;
    public Vector3 Scale = Vector3.One;
    public GameMode GameMode = gameMode;
}