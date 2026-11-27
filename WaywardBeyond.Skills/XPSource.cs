namespace WaywardBeyond.Skills;

/// <summary>The gameplay action that grants skill XP.</summary>
public enum XPSource
{
    /// <summary>The action is placing a brick.</summary>
    Place,

    /// <summary>The action is breaking a brick.</summary>
    Break,

    /// <summary>The action is manually crafting a recipe.</summary>
    Craft,

    /// <summary>The action is processing a recipe in a station.</summary>
    Process,
}
