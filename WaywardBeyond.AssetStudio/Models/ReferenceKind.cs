namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Specifies the grammar and target pool for a reference picker field.
/// </summary>
public enum ReferenceKind
{
    /// <summary>
    ///     Texture path retaining file extension.
    /// </summary>
    Texture,

    /// <summary>
    ///     Material path without extension.
    /// </summary>
    Material,

    /// <summary>
    ///     Mesh path retaining .obj extension.
    /// </summary>
    Mesh,

    /// <summary>
    ///     Bare brick albedo layer stem name.
    /// </summary>
    BrickFace,

    /// <summary>
    ///     Audio sound tag or clip path.
    /// </summary>
    Audio,

    /// <summary>
    ///     Dot-separated localization translation key.
    /// </summary>
    Localization,

    /// <summary>
    ///     Namespaced brick asset identifier.
    /// </summary>
    BrickId,

    /// <summary>
    ///     Bundled blueprint identifier relative to blueprints/ without .wbp extension.
    /// </summary>
    BlueprintId,

    /// <summary>
    ///     Invariant or localized tag list identifier.
    /// </summary>
    Tag,

    /// <summary>
    ///     Invariant sound tag identifier defined in sound tag tables.
    /// </summary>
    SoundTag,

    /// <summary>
    ///     Namespaced item asset identifier.
    /// </summary>
    ItemId,

    /// <summary>
    ///     Namespaced inventory slot group identifier.
    /// </summary>
    SlotGroupId,
}
