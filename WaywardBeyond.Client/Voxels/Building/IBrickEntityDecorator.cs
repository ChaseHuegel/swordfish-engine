using Krill;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Voxels.Models;

namespace WaywardBeyond.Client.Voxels.Building;

/// <summary>Client-side decorator hook that attaches or updates visual components on a brick entity.</summary>
public interface IBrickEntityDecorator
{
    /// <summary>Returns whether this decorator applies to <paramref name="brick"/>.</summary>
    bool AppliesTo(Brick brick);

    /// <summary>Applies or updates this decorator's components on <paramref name="entity"/>.</summary>
    void Process(in DataStore store, in int parent, in int entity, in VoxelComponent voxelComponent, in VoxelInfo voxelInfo, Brick brick);

    /// <summary>Removes this decorator's components when <paramref name="entity"/> is updated to a brick this decorator no longer applies to.</summary>
    void OnRemove(in DataStore store, in int entity)
    {
    }
}
