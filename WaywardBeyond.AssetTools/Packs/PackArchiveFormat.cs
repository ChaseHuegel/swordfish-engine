namespace WaywardBeyond.AssetTools.Packs;

/// <summary>
///     Archive container format produced when exporting a content pack.
/// </summary>
public enum PackArchiveFormat
{
    /// <summary>
    ///     Select the default archive format (<c>.zip</c> on all platforms).
    /// </summary>
    Auto = 0,

    /// <summary>
    ///     Write a <c>.zip</c> archive.
    /// </summary>
    Zip = 1,

    /// <summary>
    ///     Write a gzip-compressed <c>.tar.gz</c> archive.
    /// </summary>
    TarGz = 2
}
