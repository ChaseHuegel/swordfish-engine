using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Layers.Skills;

/// <summary>Display data for the modal skill panel.</summary>
internal readonly record struct SkillsPanelState(
    IReadOnlyList<SkillCategoryGroupState> Categories,
    int TotalLevel
)
{
    /// <summary>Gets whether the panel has at least one skill category to display.</summary>
    public bool HasSkills => Categories.Count > 0;
}
