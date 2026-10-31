using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Processing;

/// <summary>
/// Combines the depth-state ambient test with the residency boundary test.
/// </summary>
internal static class VoxelAmbient
{
    /// <summary>Returns true when a resident air cell receives undecayed ambient light.</summary>
    public static bool IsAmbient(VoxelObject voxelObject, DepthState depthState, int x, int y, int z)
    {
        return depthState.IsAmbientSource(x, y, z) || voxelObject.IsAdjacentToNonResident(x, y, z);
    }
}
