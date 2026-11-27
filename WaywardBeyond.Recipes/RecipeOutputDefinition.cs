using Tomlet.Attributes;

namespace WaywardBeyond.Recipes;

/// <summary>TOML schema for a single recipe output entry.</summary>
public struct RecipeOutputDefinition()
{
    /// <summary>The produced item identifier.</summary>
    [TomlProperty("item_id")]
    public string? ItemId { get; set; }

    /// <summary>The produced item count.</summary>
    [TomlProperty("count")]
    public int Count { get; set; }

    /// <summary>The optional output drop probability in (0, 1].</summary>
    [TomlProperty("chance")]
    public float? Chance { get; set; }
}
