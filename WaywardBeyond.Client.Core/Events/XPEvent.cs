using WaywardBeyond.Client.Core.Skills;

namespace WaywardBeyond.Client.Core.Events;

internal readonly struct XPEvent(Skill skill, int level, int xp, int gainedXP)
{
    public readonly Skill Skill = skill;
    public readonly int Level = level;
    public readonly int XP = xp;
    public readonly int GainedXP = gainedXP;
}