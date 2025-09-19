using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Items;

internal sealed class Item(string id, string name, Material icon, int maxStack, PlaceableDefinition? placeable, ToolDefinition? tool)
{
    public readonly string ID = id;
    public readonly string Name = name;
    public readonly Material Icon = icon;
    public readonly int MaxStack = maxStack;
    public PlaceableDefinition? Placeable = placeable;
    public ToolDefinition? Tool = tool;
}