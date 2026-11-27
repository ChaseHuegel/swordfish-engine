namespace WaywardBeyond.Recipes;

/// <summary>TOML schema for a single recipe input requirement.</summary>
public struct RecipeIngredientDefinition()
{
    /// <summary>The item identifier or <c>tag:&lt;name&gt;</c> selector.</summary>
    public string? ItemId { get; set; }

    /// <summary>The required item count.</summary>
    public int Count { get; set; }

    /// <summary>The optional target slot group identifier.</summary>
    public string? SlotGroup { get; set; }
}
