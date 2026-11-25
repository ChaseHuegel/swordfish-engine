using Swordfish.Library.IO;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;

namespace WaywardBeyond.Client.Serialization;

/// <summary>Parses binary <c>.wbp</c> blueprint files into raw <see cref="BlueprintData"/>.</summary>
internal sealed class BlueprintDataParser : IFileParser<BlueprintData>
{
    /// <inheritdoc/>
    public string[] SupportedExtensions { get; } =
    [
        FileBlueprintStorage.FILE_EXTENSION,
    ];

    object IFileParser.Parse(PathInfo path) => Parse(path);

    /// <inheritdoc/>
    public BlueprintData Parse(PathInfo file)
    {
        byte[] data = file.ReadBytes();
        return BlueprintData.Deserialize(data);
    }
}
