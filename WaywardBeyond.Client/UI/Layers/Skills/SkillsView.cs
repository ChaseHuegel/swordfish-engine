using System;
using System.Globalization;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.UI.Components;

namespace WaywardBeyond.Client.UI.Layers.Skills;

/// <summary>Passive Reef view for the modal skill panel.</summary>
internal partial class SkillsView(ILocalization localization, ScrollAreaState scroll) : UiViewBase(localization)
{
    /// <summary>Gets the scroll state for the skill list.</summary>
    public ScrollAreaState Scroll { get; } = scroll;

    /// <summary>Raised when the close action is triggered.</summary>
    public Action? CloseRequested { get; set; }

    /// <summary>Formats the total skill level summary line.</summary>
    protected string TotalLevelText(int totalLevel)
    {
        return string.Format(CultureInfo.InvariantCulture, L("ui.label.skill.totalLevel"), totalLevel);
    }

    /// <summary>Formats a skill's current level badge.</summary>
    protected string LevelText(int level)
    {
        return string.Format(CultureInfo.InvariantCulture, L("ui.label.skill.level"), level);
    }

    /// <summary>Formats a skill's XP earned into the current level.</summary>
    protected string LevelXpText(in SkillRowState skill)
    {
        if (skill.IsMaxLevel)
        {
            return string.Format(CultureInfo.InvariantCulture, L("ui.label.skill.xpMax"), skill.XPIntoLevel);
        }

        return string.Format(CultureInfo.InvariantCulture, L("ui.label.skill.xpProgress"), skill.XPIntoLevel, skill.NextLevelXP);
    }

    /// <summary>Formats a skill's lifetime total XP.</summary>
    protected string TotalXpText(long totalXp)
    {
        return string.Format(CultureInfo.InvariantCulture, L("ui.label.skill.totalXp"), totalXp);
    }

    /// <summary>Builds the progress bar state for one skill row.</summary>
    protected static ProgressBarState ProgressTrack(in SkillRowState skill)
    {
        return new ProgressBarState(skill.Progress);
    }

    partial void OnClose() => CloseRequested?.Invoke();
}
