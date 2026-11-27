namespace WaywardBeyond.Skills;

/// <summary>The resolved level, XP totals, next-level requirement, and normalized bar progress for a skill.</summary>
public readonly record struct SkillProgress(
    int Level,
    long TotalXP,
    long XPIntoLevel,
    int NextLevelXP,
    float Progress,
    bool IsMaxLevel
);
