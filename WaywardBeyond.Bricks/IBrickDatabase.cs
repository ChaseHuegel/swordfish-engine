using System;
using System.Collections.Generic;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Data;

namespace WaywardBeyond.Bricks;

/// <summary>Provides access to all loaded <see cref="Brick"/>s.</summary>
public interface IBrickDatabase : IAssetDatabase<Brick>
{
    /// <summary>Returns whether a block-shaped voxel of the provided shape culls faces around it.</summary>
    bool IsCuller(in Voxel voxel, BrickShape shape);

    /// <summary>Attempts to get a brick's info by its data id.</summary>
    Result<Brick> Get(ushort id);

    /// <summary>Attempts to get all brick infos that match a predicate.</summary>
    List<Brick> Get(Func<Brick, bool> predicate);
}