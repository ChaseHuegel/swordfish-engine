namespace WaywardBeyond.Recipes;

/// <summary>An output item stack produced by a <see cref="Recipe"/>.</summary>
public readonly record struct RecipeOutput(string ItemId, int Count, float Chance = 1f);
