using Swordfish.Library.IO;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Serialization;

/// <summary>Parses binary <c>.dat</c> blueprint files into raw <see cref="VoxelEntityData"/>.</summary>
internal sealed class VoxelEntityModelParser : IFileParser<VoxelEntityData>
{
    /// <inheritdoc/>
    public string[] SupportedExtensions { get; } =
    [
        ".dat",
    ];

    object IFileParser.Parse(PathInfo path) => Parse(path);

    /// <inheritdoc/>
    public VoxelEntityData Parse(PathInfo file)
    {
        byte[] data = file.ReadBytes();
        return VoxelEntityData.Deserialize(data);
    }
}