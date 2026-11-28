namespace WaywardBeyond.Data;

/// <summary>The current data format version of every serialized game record.</summary>
public static class DataVersion
{
    /// <summary>The data format version stamped into new records and targeted by migrations.</summary>
    public const uint CURRENT = 4;
}
