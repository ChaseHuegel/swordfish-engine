namespace WaywardBeyond.Recipes;

/// <summary>Determines whether a recipe starts automatically from inputs or requires explicit selection.</summary>
public enum RecipeMode
{
    /// <summary>Starts automatically when matching inputs are present.</summary>
    Auto,

    /// <summary>Requires explicit player or controller selection.</summary>
    Manual,
}
