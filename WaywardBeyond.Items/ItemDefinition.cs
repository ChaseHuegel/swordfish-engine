namespace WaywardBeyond.Items;

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
}