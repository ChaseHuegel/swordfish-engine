using WaywardBeyond.Client.Core.Skills;

namespace WaywardBeyond.Client.Core.Events;

internal readonly struct LevelUpEvent(Skill skill, int previousLevel, int level)
{
    public readonly Skill Skill = skill;
    public readonly int PreviousLevel = previousLevel;
    public readonly int Level = level;
}