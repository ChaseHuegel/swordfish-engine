namespace WaywardBeyond.Client.Core.Items;

public struct ItemDefinition()
{
    public string ID;
    public string Name;
    public string? Icon;
    public int? MaxStack;
    public PlaceableDefinition? Placeable;
    public ToolDefinition? Tool;
    public ModelDefinition? ViewModel;
    public ModelDefinition? WorldModel;
}