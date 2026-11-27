using System.Collections.Generic;
using Krill;

namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>
/// Runtime per-skill XP totals attached to a player entity on both client and server. Never persisted or
/// replicated directly; seeded from character save statistics and updated from authoritative skill grants.
/// </summary>
public readonly struct SkillStateComponent(Dictionary<string, long> xpBySkillId) : IDataComponent
{
    /// <summary>The underlying per-skill XP totals keyed by skill id, or null on a default instance.</summary>
    public IReadOnlyDictionary<string, long>? XPBySkill => xpBySkillId;

    /// <summary>Gets the total XP recorded for a skill, or 0 when none is recorded.</summary>
    public long GetXP(string skillID)
    {
        if (xpBySkillId == null || string.IsNullOrEmpty(skillID))
        {
            return 0;
        }

        return xpBySkillId.TryGetValue(skillID, out long xp) ? xp : 0;
    }

    /// <summary>Sets the total XP recorded for a skill.</summary>
    public void SetXP(string skillID, long xp)
    {
        if (xpBySkillId == null || string.IsNullOrEmpty(skillID))
        {
            return;
        }

        xpBySkillId[skillID] = xp;
    }
}
