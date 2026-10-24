using System.Collections.Generic;
using Swordfish.ECS;

namespace WaywardBeyond.Server.Components;

/// <summary>
/// Session-scoped, server-authoritative XP per skill for a player. Never persisted or replicated. The
/// map is borrowed from the caller, so a default instance is invalid and throws on access.
/// </summary>
public readonly struct SkillStateComponent(Dictionary<string, long> xpBySkillId) : IDataComponent
{
    /// <summary>Gets the total XP recorded for a skill, or 0 when none is recorded.</summary>
    public long GetXP(string skillID)
    {
        return xpBySkillId.TryGetValue(skillID, out long xp) ? xp : 0;
    }

    /// <summary>Sets the total XP recorded for a skill.</summary>
    public void SetXP(string skillID, long xp)
    {
        xpBySkillId[skillID] = xp;
    }
}
