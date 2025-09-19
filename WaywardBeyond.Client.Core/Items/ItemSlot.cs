namespace WaywardBeyond.Client.Core.Items;

internal readonly struct ItemSlot(in int slot, in Item item)
{
    public readonly int Slot = slot;
    public readonly Item Item = item;
}