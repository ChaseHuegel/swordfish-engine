namespace WaywardBeyond.Skills;

/// <summary>The resolved level for a total XP amount, plus the XP earned into that level.</summary>
public readonly record struct LevelInfo(int Level, long XPIntoLevel);
