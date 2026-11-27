using System.Collections.Generic;

namespace WaywardBeyond.Recipes;

/// <summary>A collection of <see cref="RecipeDefinition"/> entries parsed from TOML.</summary>
public struct RecipeDefinitions()
{
    /// <summary>The contained <see cref="RecipeDefinition"/> entries.</summary>
    public List<RecipeDefinition>? Recipes { get; set; }
}
