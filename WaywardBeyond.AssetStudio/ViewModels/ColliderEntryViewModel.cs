using CommunityToolkit.Mvvm.ComponentModel;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents one editable collider shape entry on a brick definition.
/// </summary>
public sealed partial class ColliderEntryViewModel : ObservableObject
{
    private readonly Action? _onChanged;
    private readonly Action<ColliderEntryViewModel, string, string, string>? _onPropertyTransition;
    private string _colliderType;
    private string _size;
    private string _offset;
    private string _rotation;

    /// <summary>
    ///     Initializes a brick collider entry with geometry fields.
    /// </summary>
    public ColliderEntryViewModel(
        string colliderType,
        string size,
        string offset,
        string rotation,
        Action? onChanged = null,
        Action<ColliderEntryViewModel, string, string, string>? onPropertyTransition = null)
    {
        _colliderType = colliderType ?? string.Empty;
        _size = size ?? string.Empty;
        _offset = offset ?? string.Empty;
        _rotation = rotation ?? string.Empty;
        _onChanged = onChanged;
        _onPropertyTransition = onPropertyTransition;
    }

    /// <summary>
    ///     Supported collider primitive type options.
    /// </summary>
    public static IReadOnlyList<string> COLLIDER_TYPES { get; } = ["box", "sphere", "capsule", "cylinder", "hull"];

    /// <summary>
    ///     Collider primitive type identifier.
    /// </summary>
    public string ColliderType
    {
        get => _colliderType;
        set => UpdateField(ref _colliderType, value, nameof(ColliderType));
    }

    /// <summary>
    ///     Collider size vector formatted as comma-separated floats.
    /// </summary>
    public string Size
    {
        get => _size;
        set => UpdateField(ref _size, value, nameof(Size));
    }

    /// <summary>
    ///     Collider offset vector formatted as comma-separated floats.
    /// </summary>
    public string Offset
    {
        get => _offset;
        set => UpdateField(ref _offset, value, nameof(Offset));
    }

    /// <summary>
    ///     Collider Euler rotation formatted as comma-separated degrees.
    /// </summary>
    public string Rotation
    {
        get => _rotation;
        set => UpdateField(ref _rotation, value, nameof(Rotation));
    }

    /// <summary>
    ///     Restores a named property during undo or redo replay without recording a new edit.
    /// </summary>
    public void ApplyPropertyForReplay(string propertyName, string restoredValue)
    {
        var safe = restoredValue ?? string.Empty;
        switch (propertyName)
        {
            case nameof(ColliderType):
                SetProperty(ref _colliderType, safe, nameof(ColliderType));
                break;
            case nameof(Size):
                SetProperty(ref _size, safe, nameof(Size));
                break;
            case nameof(Offset):
                SetProperty(ref _offset, safe, nameof(Offset));
                break;
            case nameof(Rotation):
                SetProperty(ref _rotation, safe, nameof(Rotation));
                break;
        }
    }

    private void UpdateField(ref string field, string? incoming, string propertyName)
    {
        var safe = incoming ?? string.Empty;
        var oldValue = field;
        if (!SetProperty(ref field, safe, propertyName))
        {
            return;
        }

        _onPropertyTransition?.Invoke(this, propertyName, oldValue, safe);
        _onChanged?.Invoke();
    }
}
