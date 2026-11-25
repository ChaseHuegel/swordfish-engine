namespace WaywardBeyond.Data.Networking;

/// <summary>Conversion helpers between flattened region RPC messages and <see cref="VoxelRegionData"/>.</summary>
public static class RegionMessageExtensions
{
    /// <summary>Extracts the region payload from a <see cref="PasteRegionRequest"/>.</summary>
    public static VoxelRegionData ToRegion(this in PasteRegionRequest request)
    {
        return new VoxelRegionData(
            request.SizeX,
            request.SizeY,
            request.SizeZ,
            request.Chunks ?? [],
            request.BrickPalette,
            request.PaintPalette);
    }

    /// <summary>Extracts the region payload from a <see cref="VoxelRegionEditMessage"/>.</summary>
    public static VoxelRegionData ToRegion(this in VoxelRegionEditMessage message)
    {
        return new VoxelRegionData(
            message.SizeX,
            message.SizeY,
            message.SizeZ,
            message.Chunks ?? [],
            message.BrickPalette,
            message.PaintPalette);
    }
}
