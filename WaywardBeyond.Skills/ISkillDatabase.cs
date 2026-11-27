using System.Collections.Generic;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Skills;

/// <summary>Provides access to the loaded <see cref="Skill"/> definitions.</summary>
public interface ISkillDatabase : IAssetDatabase<Skill>
{
    /// <summary>Gets all loaded skills in deterministic ID order.</summary>
    IReadOnlyList<Skill> All { get; }

    /// <summary>Gets every skill that grants XP from the provided <see cref="XPSource"/>.</summary>
    IReadOnlyList<Skill> Get(XPSource xpSource);
}
