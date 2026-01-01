namespace WaywardBeyond.Client.Core.Items;

internal struct ItemStack
{
    public static readonly ItemStack Empty = new();
    
    public readonly string ID;
    public int Count;

    public ItemStack(string id)
    {
        ID = id;
        Count = 1;
    }
    
    public ItemStack(string id, int count)
    {
        ID = id;
        Count = count;
    }
}