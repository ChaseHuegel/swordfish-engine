namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>Ordering band for <see cref="IStatModifierSource"/> evaluation.</summary>
public enum StatModifierPriority : byte
{
    /// <summary>Runs before normal sources, typically for base-setting sources such as items.</summary>
    High = 0,

    /// <summary>Standard band for gameplay modifiers such as entity stats, skills, and buffs.</summary>
    Normal = 1,

    /// <summary>Runs last, typically for caps, clamps, or final overrides.</summary>
    Low = 2,
}
