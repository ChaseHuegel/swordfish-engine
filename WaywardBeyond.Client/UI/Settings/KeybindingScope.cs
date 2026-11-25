namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Identifies when a keybinding is active so mutually exclusive contexts do not conflict.</summary>
internal enum KeybindingScope
{
    /// <summary>Active across all contexts, conflicting with both world and inventory bindings.</summary>
    Global,

    /// <summary>Active during world gameplay and blocked while modal inventory is open.</summary>
    World,

    /// <summary>Active only while the modal inventory overlay is open.</summary>
    Inventory,

    /// <summary>Active only while the chat input overlay is open.</summary>
    Chat,
}
