namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Identifies when a keybinding is active so mutually exclusive contexts do not conflict.</summary>
internal enum KeybindingScope
{
    /// <summary>Active across all contexts, conflicting with every other scope.</summary>
    Global,

    /// <summary>Active during world gameplay regardless of equipped item, blocked while modal overlays are open.</summary>
    World,

    /// <summary>Active during world gameplay except while holding a Blueprint Projector or blueprint item.</summary>
    WorldInteraction,

    /// <summary>Active during world gameplay only while holding a placeable brick.</summary>
    BrickPlacement,

    /// <summary>Active during world gameplay only while holding the paint sprayer.</summary>
    PaintSprayer,

    /// <summary>Active during world gameplay only while holding the drill.</summary>
    Drill,

    /// <summary>Active during world gameplay only while holding the Blueprint Projector.</summary>
    BlueprintTool,

    /// <summary>Active only while the modal inventory overlay is open.</summary>
    Inventory,

    /// <summary>Active only while the chat input overlay is open.</summary>
    Chat,
}
