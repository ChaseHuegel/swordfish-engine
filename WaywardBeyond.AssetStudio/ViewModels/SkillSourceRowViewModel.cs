using CommunityToolkit.Mvvm.ComponentModel;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents one XP source mapping row on a skill definition.
/// </summary>
public sealed partial class SkillSourceRowViewModel : ObservableObject
{
    private readonly Action? _onChanged;
    private readonly Action<SkillSourceRowViewModel, string, string, string>? _onPropertyTransition;
    private string _trigger;
    private string _target;
    private int _xpAmount;

    /// <summary>
    ///     Initializes a skill XP source mapping row.
    /// </summary>
    public SkillSourceRowViewModel(
        string trigger,
        string target,
        int xpAmount,
        Action? onChanged = null,
        Action<SkillSourceRowViewModel, string, string, string>? onPropertyTransition = null)
    {
        _trigger = trigger ?? string.Empty;
        _target = target ?? string.Empty;
        _xpAmount = xpAmount;
        _onChanged = onChanged;
        _onPropertyTransition = onPropertyTransition;
    }

    /// <summary>
    ///     Supported XP source trigger kinds.
    /// </summary>
    public static IReadOnlyList<string> TRIGGERS { get; } = ["Place", "Break"];

    /// <summary>
    ///     Trigger event awarding XP (Place or Break).
    /// </summary>
    public string Trigger
    {
        get => _trigger;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _trigger;
            if (!SetProperty(ref _trigger, safe))
            {
                return;
            }

            _onPropertyTransition?.Invoke(this, nameof(Trigger), oldValue, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Target brick ID or tag selector awarding XP.
    /// </summary>
    public string Target
    {
        get => _target;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _target;
            if (!SetProperty(ref _target, safe))
            {
                return;
            }

            _onPropertyTransition?.Invoke(this, nameof(Target), oldValue, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     XP amount awarded when this trigger fires.
    /// </summary>
    public int XpAmount
    {
        get => _xpAmount;
        set
        {
            var oldValue = _xpAmount;
            if (!SetProperty(ref _xpAmount, value))
            {
                return;
            }

            _onPropertyTransition?.Invoke(this, nameof(XpAmount), oldValue.ToString(), value.ToString());
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Restores a named property during undo or redo replay without pushing another edit.
    /// </summary>
    public void ApplyPropertyForReplay(string propertyName, string restoredValue)
    {
        switch (propertyName)
        {
            case nameof(Trigger):
                SetProperty(ref _trigger, restoredValue ?? string.Empty, nameof(Trigger));
                break;
            case nameof(Target):
                SetProperty(ref _target, restoredValue ?? string.Empty, nameof(Target));
                break;
            case nameof(XpAmount):
                if (int.TryParse(restoredValue, out var xp))
                {
                    SetProperty(ref _xpAmount, xp, nameof(XpAmount));
                }

                break;
        }
    }
}
