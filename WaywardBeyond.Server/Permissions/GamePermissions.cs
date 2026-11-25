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

    /// <summary>Cut a selected voxel region on an existing structure.</summary>
    public const string REGION_CUT = "waywardbeyond.region.cut";

    /// <summary>Paste a voxel region or blueprint into an existing structure.</summary>
    public const string REGION_PASTE = "waywardbeyond.region.paste";

    /// <summary>Paste a voxel region or blueprint as a new standalone structure.</summary>
    public const string REGION_SPAWN = "waywardbeyond.region.spawn";
}
