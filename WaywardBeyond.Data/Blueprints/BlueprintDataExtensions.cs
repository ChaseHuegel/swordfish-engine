namespace WaywardBeyond.Data.Blueprints;

/// <summary>Conversion helpers between <see cref="BlueprintData"/> and <see cref="VoxelRegionData"/>.</summary>
public static class BlueprintDataExtensions
{
    /// <summary>Extracts the region payload of a <see cref="BlueprintData"/> into a <see cref="VoxelRegionData"/>.</summary>
    public static VoxelRegionData ToRegion(this in BlueprintData data)
    {
        return new VoxelRegionData(
            data.SizeX,
            data.SizeY,
            data.SizeZ,
            data.Chunks ?? [],
            data.BrickPalette,
            data.PaintPalette);
    }
}
