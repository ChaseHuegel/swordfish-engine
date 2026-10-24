namespace WaywardBeyond.Items;

/// <summary>
/// The headless view of a loaded item: identity, stacking, and the content it places or targets. Render
/// assets stay client-side; the item carries only the ids of its icon and model.
/// </summary>
public sealed class Item(
    string id,
    string name,
    string? iconID,
    int maxStack,
    PlaceableDefinition? placeable,
    ToolDefinition? tool,
    ModelDefinition? viewModel,
    ModelDefinition? worldModel
) {
    /// <summary>The canonical namespaced item id.</summary>
    public readonly string ID = id;

    /// <summary>The localization key of the display name.</summary>
    public readonly string Name = name;

    /// <summary>The texture id of the inventory icon, or null when the item has none.</summary>
    public readonly string? IconID = iconID;

    /// <summary>The largest stack one slot holds.</summary>
    public readonly int MaxStack = maxStack;

    /// <summary>The content the item places, or null when it is not placeable.</summary>
    public readonly PlaceableDefinition? Placeable = placeable;

    /// <summary>The tool behavior the item carries, or null when it is not a tool.</summary>
    public readonly ToolDefinition? Tool = tool;

    /// <summary>The view-model render ids, or null when the item has none.</summary>
    public readonly ModelDefinition? ViewModel = viewModel;

    /// <summary>The world-model render ids, or null when the item has none.</summary>
    public readonly ModelDefinition? WorldModel = worldModel;
}
