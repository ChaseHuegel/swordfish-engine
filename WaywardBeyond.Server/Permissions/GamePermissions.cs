namespace WaywardBeyond.Server.Permissions;

/// <summary>Permission keys for built-in game features. Mods declare their own keys.</summary>
public static class GamePermissions
{
    /// <summary>Trigger a manual level save.</summary>
    public const string LevelSave = "waywardbeyond.level.save";

    /// <summary>Create a new saved level.</summary>
    public const string LevelCreate = "waywardbeyond.level.create";
}
