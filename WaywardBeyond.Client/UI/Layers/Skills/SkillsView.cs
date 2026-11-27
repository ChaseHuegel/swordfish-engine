using System;
using System.Globalization;
using Reef.Constraints;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.UI.Components;

namespace WaywardBeyond.Client.UI.Layers.Skills;

/// <summary>Passive Reef view for the modal skill panel.</summary>
internal partial class SkillsView(ILocalization localization, ScrollAreaState scroll) : UiViewBase(localization)
{
    private const int EMPTY_PANEL_HEIGHT = 180;
    private const int PANEL_CHROME_HEIGHT = 91;
    private const int CATEGORY_HEADER_HEIGHT = 25;
    private const int CATEGORY_GAP = 10;
    private const int SKILL_ROW_HEIGHT = 70;
    private const int MAX_PANEL_HEIGHT = 520;
    private const int VIEWPORT_VERTICAL_MARGIN = 160;
    private const int MIN_CLAMPED_HEIGHT = 220;
    private const int BAR_SEGMENTS = 10;
    private const int SEGMENT_GAP = 2;
    private const int TRACK_WIDTH = 406;
    private const int SCROLLABLE_TRACK_WIDTH = 392;

    private static readonly int[] _dividers = CreateDividerPositions(TRACK_WIDTH);
    private static readonly int[] _scrollableDividers = CreateDividerPositions(SCROLLABLE_TRACK_WIDTH);

    /// <summary>Gets the scroll state for the skill list.</summary>
    public ScrollAreaState Scroll { get; } = scroll;

    /// <summary>Raised when the close action is triggered.</summary>
    public Action? CloseRequested { get; set; }

    /// <summary>Computes the panel height constraint tightly around its content up to the scroll cap.</summary>
    protected static IConstraint PanelHeightConstraint(in SkillsPanelState state, int viewportHeight)
    {
        if (!state.HasSkills)
        {
            return new Fixed(EMPTY_PANEL_HEIGHT);
        }

        int desiredHeight = PANEL_CHROME_HEIGHT + EstimateContentHeight(in state);
        int maxHeight = MaxPanelHeight(viewportHeight);
        return new Fixed(Math.Min(desiredHeight, maxHeight));
    }

    /// <summary>Returns whether the skill list exceeds the clamped panel height and needs a scrollbar.</summary>
    protected static bool NeedsScroll(in SkillsPanelState state, int viewportHeight)
    {
        if (!state.HasSkills)
        {
            return false;
        }

        return PANEL_CHROME_HEIGHT + EstimateContentHeight(in state) > MaxPanelHeight(viewportHeight);
    }

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

    /// <summary>Returns the divider x-offsets for a 10-segment skill progress bar.</summary>
    protected static int[] DividerPositions(in SkillsPanelState state, int viewportHeight)
    {
        return NeedsScroll(in state, viewportHeight) ? _scrollableDividers : _dividers;
    }

    private static int[] CreateDividerPositions(int trackWidth)
    {
        var dividers = new int[BAR_SEGMENTS - 1];
        for (var segmentIndex = 1; segmentIndex < BAR_SEGMENTS; segmentIndex++)
        {
            dividers[segmentIndex - 1] = trackWidth * segmentIndex / BAR_SEGMENTS - SEGMENT_GAP / 2;
        }

        return dividers;
    }

    private static int EstimateContentHeight(in SkillsPanelState state)
    {
        int categories = state.Categories.Count;
        var skills = 0;
        for (var i = 0; i < categories; i++)
        {
            skills += state.Categories[i].Skills.Count;
        }

        return categories * CATEGORY_HEADER_HEIGHT + skills * SKILL_ROW_HEIGHT + Math.Max(0, categories - 1) * CATEGORY_GAP;
    }

    private static int MaxPanelHeight(int viewportHeight)
    {
        int available = Math.Max(MIN_CLAMPED_HEIGHT, viewportHeight - VIEWPORT_VERTICAL_MARGIN);
        return Math.Min(MAX_PANEL_HEIGHT, available);
    }

    partial void OnClose() => CloseRequested?.Invoke();
}
