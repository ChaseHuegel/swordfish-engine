namespace WaywardBeyond.Shared.Skills;

/// <summary>
/// The resolved level a skill is at for a given total XP, plus the XP earned into the current level.
/// </summary>
public record struct LevelInfo(int Level, long XP);