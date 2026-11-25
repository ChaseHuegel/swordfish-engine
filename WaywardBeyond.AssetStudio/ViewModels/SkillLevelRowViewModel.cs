using CommunityToolkit.Mvvm.ComponentModel;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents one level threshold row in a skill XP curve table.
/// </summary>
public sealed partial class SkillLevelRowViewModel : ObservableObject
{
    private readonly Action? _onChanged;
    private readonly Action<SkillLevelRowViewModel, int, int>? _onXpTransition;
    private int _xpRequired;

    [ObservableProperty]
    private int _level;

    [ObservableProperty]
    private int _cumulativeXp;

    /// <summary>
    ///     Initializes a skill level row with incremental and cumulative XP values.
    /// </summary>
    public SkillLevelRowViewModel(
        int level,
        int xpRequired,
        int cumulativeXp,
        Action? onChanged = null,
        Action<SkillLevelRowViewModel, int, int>? onXpTransition = null)
    {
        _level = level;
        _xpRequired = xpRequired;
        _cumulativeXp = cumulativeXp;
        _onChanged = onChanged;
        _onXpTransition = onXpTransition;
    }

    /// <summary>
    ///     Incremental XP required to reach this level.
    /// </summary>
    public int XpRequired
    {
        get => _xpRequired;
        set
        {
            var oldValue = _xpRequired;
            if (!SetProperty(ref _xpRequired, value))
            {
                return;
            }

            _onXpTransition?.Invoke(this, oldValue, value);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Restores the incremental XP requirement during undo or redo replay.
    /// </summary>
    public void ApplyXpForReplay(int restoredXp)
    {
        SetProperty(ref _xpRequired, restoredXp, nameof(XpRequired));
    }
}
