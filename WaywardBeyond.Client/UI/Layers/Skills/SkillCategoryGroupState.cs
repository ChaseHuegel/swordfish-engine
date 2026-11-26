using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Layers.Skills;

/// <summary>Display data for one skill category group in the skill panel.</summary>
internal readonly record struct SkillCategoryGroupState(
    string CategoryKey,
    IReadOnlyList<SkillRowState> Skills
);
