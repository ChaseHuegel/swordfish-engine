using CommunityToolkit.Mvvm.ComponentModel;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents one editable inventory slot group row under <c>[[Bricks.Entity.SlotGroups]]</c>.
/// </summary>
public sealed partial class BrickSlotGroupRowViewModel : ObservableObject
{
    private readonly Action? _onChanged;
    private readonly Action<BrickSlotGroupRowViewModel, string, string, string>? _onPropertyTransition;
    private string _groupId;
    private string _capacity;

    /// <summary>
    ///     Initializes a brick entity slot group row.
    /// </summary>
    public BrickSlotGroupRowViewModel(
        string groupId,
        string capacity,
        Action? onChanged = null,
        Action<BrickSlotGroupRowViewModel, string, string, string>? onPropertyTransition = null,
        bool isReadOnly = false)
    {
        _groupId = groupId ?? string.Empty;
        _capacity = capacity ?? string.Empty;
        _onChanged = onChanged;
        _onPropertyTransition = onPropertyTransition;
        IsReadOnly = isReadOnly;
    }

    /// <summary>
    ///     Whether this row belongs to a read-only document.
    /// </summary>
    public bool IsReadOnly { get; }

    /// <summary>
    ///     Namespaced slot group identifier (for example <c>wb:cargo</c> or <c>wb:fuel</c>).
    /// </summary>
    public string GroupId
    {
        get => _groupId;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _groupId;
            if (!SetProperty(ref _groupId, safe))
            {
                return;
            }

            _onPropertyTransition?.Invoke(this, nameof(GroupId), oldValue, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Optional non-negative integer capacity override, or empty to use the schema default.
    /// </summary>
    public string Capacity
    {
        get => _capacity;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _capacity;
            if (!SetProperty(ref _capacity, safe))
            {
                return;
            }

            _onPropertyTransition?.Invoke(this, nameof(Capacity), oldValue, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Restores a named property during undo or redo replay without pushing another edit.
    /// </summary>
    public void ApplyPropertyForReplay(string propertyName, string restoredValue)
    {
        var safe = restoredValue ?? string.Empty;
        switch (propertyName)
        {
            case nameof(GroupId):
                SetProperty(ref _groupId, safe, nameof(GroupId));
                break;
            case nameof(Capacity):
                SetProperty(ref _capacity, safe, nameof(Capacity));
                break;
        }
    }
}
