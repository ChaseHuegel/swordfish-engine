namespace WaywardBeyond.Client.Core.Items;

internal struct ItemDefinition
{
    public string ID;
    public string Name;
    public string? Icon;
    public int? MaxStack;
    public PlaceableDefinition? Placeable;
}