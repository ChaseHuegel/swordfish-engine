using System.Collections.Generic;
using Swordfish.ECS;

namespace WaywardBeyond.Server.Components;

/// <summary>Session-scoped, server-authoritative XP per skill for a player. Never persisted or replicated.</summary>
public struct SkillStateComponent(Dictionary<string, long> xpBySkillId) : IDataComponent
{
    private Dictionary<string, long> _xpBySkillId = xpBySkillId;

    /// <summary>Gets the total XP recorded for a skill, or 0 when none is recorded.</summary>
    public long GetXP(string skillID)
    {
        return _xpBySkillId != null && _xpBySkillId.TryGetValue(skillID, out long xp) ? xp : 0;
    }

    /// <summary>Sets the total XP recorded for a skill.</summary>
    public void SetXP(string skillID, long xp)
    {
        _xpBySkillId ??= [];
        _xpBySkillId[skillID] = xp;
    }
}