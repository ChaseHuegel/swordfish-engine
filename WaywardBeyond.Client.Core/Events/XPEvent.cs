using WaywardBeyond.Client.Core.Skills;

namespace WaywardBeyond.Client.Core.Events;

internal readonly struct XPEvent(Skill skill, int level, long xp, long gainedXP)
{
    public readonly Skill Skill = skill;
    public readonly int Level = level;
    public readonly long XP = xp;
    public readonly long GainedXP = gainedXP;
}