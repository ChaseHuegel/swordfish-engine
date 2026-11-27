using System.Collections.Generic;

namespace WaywardBeyond.Items;

/// <summary>Defines an item asset.</summary>
public struct ItemDefinition()
{
    public string ID = string.Empty;
    public string Name = string.Empty;
    public string? Description;
    public string? Icon;
    public int? MaxStack;
    public PlaceableDefinition? Placeable;
    public ToolDefinition? Tool;
    public ModelDefinition? ViewModel;
    public ModelDefinition? WorldModel;

    /// <summary>Optional custom numeric statistics declared on the item, keyed by statistic id.</summary>
    public Dictionary<string, float>? Statistics;
}
