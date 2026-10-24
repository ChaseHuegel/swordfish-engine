using Swordfish.Library.IO;
using Swordfish.Library.Serialization;
using WaywardBeyond.Client.Voxels.Models;

namespace WaywardBeyond.Client.Serialization;

internal class VoxelEntityModelParser(in ISerializer<VoxelEntityModel> serializer) : IFileParser<VoxelEntityModel>
{
    private readonly ISerializer<VoxelEntityModel> _serializer = serializer;

    public string[] SupportedExtensions { get; } =
    [
        ".dat",
    ];
    
    object IFileParser.Parse(PathInfo path) => Parse(path);
    public VoxelEntityModel Parse(PathInfo file)
    {
        byte[] data = file.ReadBytes();
        return _serializer.Deserialize(data);
    }
}