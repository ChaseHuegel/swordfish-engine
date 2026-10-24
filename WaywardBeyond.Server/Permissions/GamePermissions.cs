namespace WaywardBeyond.Server.Permissions;

/// <summary>Permission keys for built-in game features. Mods declare their own keys.</summary>
public static class GamePermissions
{
    /// <summary>Trigger a manual level save.</summary>
    public const string LEVEL_SAVE = "waywardbeyond.level.save";

    /// <summary>Create a new saved level.</summary>
    public const string LEVEL_CREATE = "waywardbeyond.level.create";

    /// <summary>Delete a saved level.</summary>
    public const string LEVEL_DELETE = "waywardbeyond.level.delete";
}
