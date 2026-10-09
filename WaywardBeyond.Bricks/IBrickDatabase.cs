using System;
using System.Collections.Generic;
using Swordfish.Library.Util;
using WaywardBeyond.Data;

namespace WaywardBeyond.Bricks;

/// <summary>
/// Headless access to brick definitions. The culling overload in this interface takes only the voxel and
/// its shape; the <see cref="ShapeLight"/>-based overloads live on the client, which owns that type.
/// </summary>
public interface IBrickDatabase
{
    /// <summary>Returns whether a block-shaped voxel of the provided shape culls faces around it.</summary>
    bool IsCuller(in Voxel voxel, BrickShape shape);

    /// <summary>Attempts to get a brick's info by its data id.</summary>
    Result<BrickInfo> Get(ushort id);

    /// <summary>Attempts to get all brick infos that match a predicate.</summary>
    List<BrickInfo> Get(Func<BrickInfo, bool> predicate);
}