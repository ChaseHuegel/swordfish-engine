using System.Collections.Generic;
using Tomlet.Attributes;

namespace WaywardBeyond.Recipes;

/// <summary>A collection of <see cref="RecipeDefinition"/> entries parsed from TOML.</summary>
public struct RecipeDefinitions()
{
    /// <summary>The contained <see cref="RecipeDefinition"/> entries under <c>[[recipes]]</c>.</summary>
    [TomlProperty("recipes")]
    public List<RecipeDefinition>? Recipes { get; set; }

    /// <summary>Fallback entries declared under PascalCase <c>[[Recipes]]</c>.</summary>
    [TomlProperty("Recipes")]
    internal List<RecipeDefinition>? PascalRecipes { get; set; }
}
