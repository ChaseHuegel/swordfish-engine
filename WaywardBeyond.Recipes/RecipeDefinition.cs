using System.Collections.Generic;
using Tomlet.Attributes;

namespace WaywardBeyond.Recipes;

/// <summary>TOML schema for a recipe definition.</summary>
public struct RecipeDefinition()
{
    /// <summary>The unique identifier of the recipe.</summary>
    [TomlProperty("id")]
    public string? ID { get; set; }

    /// <summary>The invariant station tag identifying stations that can run this recipe.</summary>
    [TomlProperty("station_tag")]
    public string? StationTag { get; set; }

    /// <summary>The recipe trigger mode (<c>auto</c> or <c>manual</c>).</summary>
    [TomlProperty("mode")]
    public string? Mode { get; set; }

    /// <summary>The required input ingredients.</summary>
    [TomlProperty("inputs")]
    public List<RecipeIngredientDefinition>? Inputs { get; set; }

    /// <summary>The produced output items.</summary>
    [TomlProperty("outputs")]
    public List<RecipeOutputDefinition>? Outputs { get; set; }

    /// <summary>The processing duration in seconds.</summary>
    [TomlProperty("duration_seconds")]
    public float DurationSeconds { get; set; }

    /// <summary>The power draw (negative) or generation (positive) in kilowatts.</summary>
    [TomlProperty("power_kw")]
    public float PowerKw { get; set; }

    /// <summary>The optional category localization or grouping key.</summary>
    [TomlProperty("category")]
    public string? Category { get; set; }

    /// <summary>The deterministic sort order within a station recipe list.</summary>
    [TomlProperty("order")]
    public int Order { get; set; }
}
