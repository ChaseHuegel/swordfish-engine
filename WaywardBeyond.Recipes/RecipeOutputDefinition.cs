namespace WaywardBeyond.Recipes;

/// <summary>TOML schema for a single recipe output entry.</summary>
public struct RecipeOutputDefinition()
{
    /// <summary>The produced item identifier.</summary>
    public string? ItemId { get; set; }

    /// <summary>The produced item count.</summary>
    public int Count { get; set; }

    /// <summary>The optional output drop probability in (0, 1].</summary>
    public float? Chance { get; set; }
}
