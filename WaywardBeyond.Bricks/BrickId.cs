namespace WaywardBeyond.Bricks;

/// <summary>
/// The canonical-id naming convention for bricks and the items that place them. The namespace prefix
/// scopes base-game ids away from mod ids; it is a convention, not brick data.
/// </summary>
public static class BrickId
{
    /// <summary>Namespace prefix for shipped (base-game) brick and item ids.</summary>
    public const string Namespace = "wb";

    /// <summary>Returns a brick id namespaced under <see cref="Namespace"/>.</summary>
    public static string Namespaced(string bareId)
    {
        return $"{Namespace}:{bareId}";
    }
}