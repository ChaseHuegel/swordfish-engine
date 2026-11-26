using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI.Layers.Skills;

/// <summary>Display data for one skill row in the skill panel.</summary>
internal readonly record struct SkillRowState(
    string ID,
    string NameKey,
    string CategoryKey,
    Material Icon,
    int Level,
    long TotalXP,
    long XPIntoLevel,
    int NextLevelXP,
    float Progress,
    bool IsMaxLevel
);
