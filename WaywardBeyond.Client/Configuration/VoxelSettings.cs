using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Client.Configuration;

public sealed class VoxelSettings : Config<VoxelSettings>
{
    /// <summary>Time budget in milliseconds for one tick of voxel chunk rebuilding.</summary>
    public DataBinding<float> RebuildBudgetMs { get; private set; } = new(10f);
}
