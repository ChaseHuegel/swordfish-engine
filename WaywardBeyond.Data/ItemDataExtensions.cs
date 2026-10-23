namespace WaywardBeyond.Data;

/// <summary>Slot predicates over <see cref="ItemData"/>.</summary>
public static class ItemDataExtensions
{
    /// <summary>Returns whether the slot holds no item. A non-positive count reads as empty.</summary>
    public static bool IsEmpty(this in ItemData item)
    {
        return item.Count <= 0 || string.IsNullOrEmpty(item.ID);
    }
}
