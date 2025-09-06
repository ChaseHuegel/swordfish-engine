using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Items;

internal class Item(string id, string name, Material icon, int maxStack, PlaceableDefinition? placeable)
{
    public readonly string ID = id;
    public readonly string Name = name;
    public readonly Material Icon = icon;
    public readonly int MaxStack = maxStack;
    public PlaceableDefinition? Placeable = placeable;
}