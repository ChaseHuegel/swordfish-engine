using System.Collections.Generic;
using Swordfish.ECS;

namespace WaywardBeyond.Shared.Skills;

/// <summary>
/// The server-authoritative, session-scoped XP per skill for a player. Seeded from the joining client's
/// character statistics (the client owns the initial seed), then written only by the server for the life
/// of the session. It is never persisted - the server stores no character skill data - and is freed with
/// the player entity.
/// </summary>
public struct SkillStateComponent : IDataComponent
{
    public Dictionary<string, long> XPBySkillId;

    public SkillStateComponent(Dictionary<string, long> xpBySkillId)
    {
        XPBySkillId = xpBySkillId;
    }

    public long GetXP(string skillID)
    {
        if (XPBySkillId != null && XPBySkillId.TryGetValue(skillID, out long xp))
        {
            return xp;
        }

        return 0;
    }

    public void SetXP(string skillID, long xp)
    {
        XPBySkillId ??= [];
        XPBySkillId[skillID] = xp;
    }
}