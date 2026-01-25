namespace WaywardBeyond.Client.Core.Items;

internal struct ItemStack
{
    public static readonly ItemStack Empty = new();
    
    public readonly string ID;
    public int Count;
    public int MaxSize;

    public ItemStack(string id, int maxSize) 
        : this(id, count: 1, maxSize) {}
    
    public ItemStack(string id, int count, int maxSize)
    {
        ID = id;
        Count = count;
        MaxSize = maxSize;
    }
}