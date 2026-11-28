namespace WaywardBeyond.Data;

/// <summary>Canonical inventory slot group identifiers and default capacities.</summary>
public static class SlotGroups
{
    /// <summary>The player hotbar slot group identifier.</summary>
    public const string HOTBAR = "wb:hotbar";

    /// <summary>The player backpack slot group identifier.</summary>
    public const string BACKPACK = "wb:backpack";

    /// <summary>The player wearable equipment slot group identifier.</summary>
    public const string WEARABLES = "wb:wearables";

    /// <summary>The player cybernetic augment slot group identifier.</summary>
    public const string AUGMENTS = "wb:augments";

    /// <summary>The container or machine input slot group identifier.</summary>
    public const string INPUT = "wb:input";

    /// <summary>The container or machine output slot group identifier.</summary>
    public const string OUTPUT = "wb:output";

    /// <summary>The container or machine fuel slot group identifier.</summary>
    public const string FUEL = "wb:fuel";

    /// <summary>The generic storage container cargo slot group identifier.</summary>
    public const string CARGO = "wb:cargo";

    /// <summary>The default number of hotbar slots.</summary>
    public const int DEFAULT_HOTBAR_CAPACITY = 9;

    /// <summary>The default number of backpack slots.</summary>
    public const int DEFAULT_BACKPACK_CAPACITY = 36;

    /// <summary>The default number of wearable equipment slots.</summary>
    public const int DEFAULT_WEARABLES_CAPACITY = 6;

    /// <summary>The default number of augment slots.</summary>
    public const int DEFAULT_AUGMENTS_CAPACITY = 5;

    /// <summary>The default number of cargo container slots.</summary>
    public const int DEFAULT_CARGO_CAPACITY = 27;
}
