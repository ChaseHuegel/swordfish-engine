namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Identifies the authoring category of a workspace document.
/// </summary>
public enum AssetKind
{
    /// <summary>
    ///     Voxel brick definition.
    /// </summary>
    Brick,

    /// <summary>
    ///     Inventory item definition.
    /// </summary>
    Item,

    /// <summary>
    ///     Color or texture paint definition.
    /// </summary>
    Paint,

    /// <summary>
    ///     Skill progression definition.
    /// </summary>
    Skill,

    /// <summary>
    ///     Character body sprite definition.
    /// </summary>
    Body,

    /// <summary>
    ///     Engine material definition.
    /// </summary>
    Material,

    /// <summary>
    ///     Invariant or localized ordered tag list.
    /// </summary>
    Tag,

    /// <summary>
    ///     Per-file localization translation table.
    /// </summary>
    Localization,

    /// <summary>
    ///     Binary blueprint structure file.
    /// </summary>
    Blueprint,

    /// <summary>
    ///     Crafting or processing recipe definition.
    /// </summary>
    Recipe,

    /// <summary>
    ///     Inventory slot group schema and per-slot filter override definition.
    /// </summary>
    SlotGroup,
}
