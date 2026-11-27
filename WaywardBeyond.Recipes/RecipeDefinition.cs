using System.Collections.Generic;

namespace WaywardBeyond.Recipes;

/// <summary>TOML schema for a recipe definition.</summary>
public struct RecipeDefinition()
{
    /// <summary>The unique identifier of the recipe.</summary>
    public string? ID { get; set; }

    /// <summary>The invariant target tag identifying stations or targets that can run this recipe.</summary>
    public string? TargetTag { get; set; }

    /// <summary>The recipe trigger mode (<c>Auto</c> or <c>Manual</c>).</summary>
    public string? Mode { get; set; }

    /// <summary>The required input ingredients.</summary>
    public List<RecipeIngredientDefinition>? Inputs { get; set; }

    /// <summary>The produced output items.</summary>
    public List<RecipeOutputDefinition>? Outputs { get; set; }

    /// <summary>The processing duration in seconds.</summary>
    public float DurationSeconds { get; set; }

    /// <summary>The power draw (negative) or generation (positive) in kilowatts.</summary>
    public float PowerKw { get; set; }

    /// <summary>The optional category localization or grouping key.</summary>
    public string? Category { get; set; }

    /// <summary>The deterministic sort order within a target recipe list.</summary>
    public int Order { get; set; }
}
