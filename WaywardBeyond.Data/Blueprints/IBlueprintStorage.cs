using System.Collections.Generic;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Blueprints;

/// <summary>Provides persistent client-local storage for player-authored blueprints.</summary>
public interface IBlueprintStorage
{
    /// <summary>Lists all valid stored blueprints ordered by modification time descending.</summary>
    Result<IReadOnlyList<BlueprintSummary>> List();

    /// <summary>Loads one stored blueprint by identifier.</summary>
    Result<BlueprintData> Load(string id);

    /// <summary>Saves a palette-encoded voxel region as a new blueprint.</summary>
    Result<BlueprintSummary> Save(string name, in VoxelRegionData encodedRegion, int brickCount);

    /// <summary>Renames an existing stored blueprint.</summary>
    Result<BlueprintSummary> Rename(string id, string newName);

    /// <summary>Deletes a stored blueprint by identifier.</summary>
    Result Delete(string id);
}
