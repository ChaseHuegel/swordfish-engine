using Swordfish.Library.Configuration;
using Tomlet.Attributes;

namespace WaywardBeyond.Data.Packs;

/// <summary>Advisory metadata stamped into a content pack's <c>pack.toml</c>.</summary>
internal sealed class ContentPackMetadata : Toml<ContentPackMetadata>
{
    /// <summary>File name of the pack metadata descriptor at the pack root.</summary>
    public const string FILE_NAME = "pack.toml";

    /// <summary>Content definition schema version that the pack targets.</summary>
    [TomlProperty("ContentVersion")]
    public uint ContentVersion { get; private set; }
}
