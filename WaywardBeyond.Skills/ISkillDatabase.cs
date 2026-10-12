using System.Collections.Generic;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Skills;

/// <summary>Provides access to the loaded <see cref="Skill"/> definitions.</summary>
public interface ISkillDatabase : IAssetDatabase<Skill>
{
    /// <summary>Gets every skill that grants XP from the provided <see cref="XPSource"/>.</summary>
    IReadOnlyList<Skill> Get(XPSource xpSource);
}
