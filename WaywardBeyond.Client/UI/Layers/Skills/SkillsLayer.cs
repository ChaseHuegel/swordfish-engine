using System;
using System.Collections.Generic;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.Skills;
using WaywardBeyond.Client.Statistics;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Data;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Client.UI.Layers.Skills;

/// <summary>Controller and presenter for the modal skill panel layer.</summary>
internal sealed class SkillsLayer : IUILayer
{
    internal const string UNCATEGORIZED_CATEGORY_KEY = "ui.label.skill.uncategorized";

    private sealed class CategoryBucket(string categoryKey, Skill[] skills)
    {
        public readonly string CategoryKey = categoryKey;
        public readonly Skill[] Skills = skills;
        public readonly SkillRowState[] Rows = new SkillRowState[skills.Length];
    }

    private readonly InteractionState _interactionState;
    private readonly SkillIconDatabase _skillIcons;
    private readonly ActiveCharacterSave _activeCharacterSave;
    private readonly ScrollAreaState _scroll = new();
    private readonly SkillsView _view;
    private readonly CategoryBucket[] _buckets;
    private readonly SkillCategoryGroupState[] _groups;

    private bool _open;
    private InteractionState.InteractionBlocker? _interactionBlocker;

    /// <summary>Initializes a new instance of the <see cref="SkillsLayer"/> class.</summary>
    public SkillsLayer(
        in IShortcutService shortcutService,
        in InteractionState interactionState,
        in ISkillDatabase skillDatabase,
        in SkillIconDatabase skillIcons,
        in ActiveCharacterSave activeCharacterSave,
        in ILocalization localization
    )
    {
        _interactionState = interactionState;
        _skillIcons = skillIcons;
        _activeCharacterSave = activeCharacterSave;
        _buckets = BuildBuckets(skillDatabase.All);
        _groups = new SkillCategoryGroupState[_buckets.Length];
        for (var i = 0; i < _buckets.Length; i++)
        {
            _groups[i] = new SkillCategoryGroupState(_buckets[i].CategoryKey, _buckets[i].Rows);
        }

        _view = new SkillsView(localization, _scroll)
        {
            CloseRequested = Close,
        };

        shortcutService.RegisterShortcut(new Shortcut
        {
            Name = "Skills",
            Category = "UI",
            Modifiers = ShortcutModifiers.None,
            Key = Key.K,
            IsEnabled = () => WaywardBeyond.IsPlaying() && (_open || !_interactionState.IsInteractionBlocked()),
            Action = Toggle,
        });

        GameShortcuts.RegisterOrAddHandler(
            shortcutService,
            GameShortcuts.CreateBack(),
            IsVisible,
            Close
        );
    }

    /// <summary>Toggles the skill panel open or closed.</summary>
    public void Toggle()
    {
        if (_open)
        {
            Close();
            return;
        }

        Open();
    }

    /// <summary>Opens the skill panel if gameplay interaction can be exclusively acquired.</summary>
    public void Open()
    {
        if (_open || !WaywardBeyond.IsPlaying())
        {
            return;
        }

        if (!_interactionState.TryBlockInteractionExclusive(out _interactionBlocker))
        {
            return;
        }

        _scroll.Reset();
        _open = true;
    }

    /// <summary>Closes the skill panel and restores gameplay input.</summary>
    public void Close()
    {
        if (!_open)
        {
            return;
        }

        _interactionBlocker?.Dispose();
        _interactionBlocker = null;
        _open = false;
    }

    /// <inheritdoc/>
    public bool IsVisible() => _open && WaywardBeyond.IsPlaying();

    /// <inheritdoc/>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        if (!WaywardBeyond.IsPlaying())
        {
            Close();
            return Result.FromSuccess();
        }

        SkillsPanelState state = BuildState();
        _view.Render(ui, state);
        _scroll.Measure(ui, "Skills.List");
        return Result.FromSuccess();
    }

    /// <summary>Updates the grouped skill row states from the active character save.</summary>
    internal SkillsPanelState BuildState()
    {
        Character? active = _activeCharacterSave.ActiveSave;
        var totalLevel = 0;

        for (var groupIndex = 0; groupIndex < _buckets.Length; groupIndex++)
        {
            CategoryBucket bucket = _buckets[groupIndex];
            for (var skillIndex = 0; skillIndex < bucket.Skills.Length; skillIndex++)
            {
                Skill skill = bucket.Skills[skillIndex];
                long totalXP = active != null ? active.Value.GetStatistic(skill.ID) : 0;
                SkillProgress progress = skill.CalculateProgress(totalXP);
                totalLevel += progress.Level;

                bucket.Rows[skillIndex] = new SkillRowState(
                    ID: skill.ID,
                    NameKey: skill.Name,
                    CategoryKey: bucket.CategoryKey,
                    Icon: _skillIcons.Get(skill),
                    Level: progress.Level,
                    TotalXP: progress.TotalXP,
                    XPIntoLevel: progress.XPIntoLevel,
                    NextLevelXP: progress.NextLevelXP,
                    Progress: progress.Progress,
                    IsMaxLevel: progress.IsMaxLevel
                );
            }
        }

        return new SkillsPanelState(_groups, totalLevel);
    }

    private static CategoryBucket[] BuildBuckets(IReadOnlyList<Skill> skills)
    {
        if (skills.Count == 0)
        {
            return [];
        }

        var grouped = new Dictionary<string, List<Skill>>(StringComparer.Ordinal);
        for (var i = 0; i < skills.Count; i++)
        {
            Skill skill = skills[i];
            string categoryKey = string.IsNullOrWhiteSpace(skill.Category)
                ? UNCATEGORIZED_CATEGORY_KEY
                : skill.Category;

            if (!grouped.TryGetValue(categoryKey, out List<Skill>? list))
            {
                list = [];
                grouped[categoryKey] = list;
            }

            list.Add(skill);
        }

        var categoryKeys = new List<string>(grouped.Keys);
        categoryKeys.Sort(StringComparer.Ordinal);

        var buckets = new CategoryBucket[categoryKeys.Count];
        for (var i = 0; i < categoryKeys.Count; i++)
        {
            string categoryKey = categoryKeys[i];
            List<Skill> groupSkills = grouped[categoryKey];
            groupSkills.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.ID, right.ID));
            buckets[i] = new CategoryBucket(categoryKey, [.. groupSkills]);
        }

        return buckets;
    }
}
