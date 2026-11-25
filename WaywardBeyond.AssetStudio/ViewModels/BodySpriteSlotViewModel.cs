using System.Diagnostics;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents one directional 2D sprite texture slot inside a body state map.
/// </summary>
public sealed partial class BodySpriteSlotViewModel : ObservableObject
{
    private readonly Action? _onChanged;
    private readonly Action<BodySpriteSlotViewModel, string, string>? _onTexturePathTransition;
    private string _texturePath;
    private string? _resolvedAbsolutePath;
    private Bitmap? _spriteBitmap;

    /// <summary>
    ///     Initializes a directional sprite slot for a body state.
    /// </summary>
    public BodySpriteSlotViewModel(
        string stateName,
        string direction,
        string texturePath,
        string swatchHex,
        Action? onChanged = null,
        string? resolvedAbsolutePath = null,
        Action<BodySpriteSlotViewModel, string, string>? onTexturePathTransition = null)
    {
        StateName = stateName;
        Direction = direction;
        _texturePath = texturePath ?? string.Empty;
        SwatchHex = swatchHex;
        _resolvedAbsolutePath = resolvedAbsolutePath;
        _onChanged = onChanged;
        _onTexturePathTransition = onTexturePathTransition;
        _spriteBitmap = TryLoadBitmap(resolvedAbsolutePath);
    }

    /// <summary>
    ///     Owning animation state name (for example standing or floating).
    /// </summary>
    public string StateName { get; }

    /// <summary>
    ///     Canonical cardinal direction key (front, back, left, right).
    /// </summary>
    public string Direction { get; }

    /// <summary>
    ///     Editable workspace-relative texture path for this sprite slot.
    /// </summary>
    public string TexturePath
    {
        get => _texturePath;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _texturePath;
            if (!SetProperty(ref _texturePath, safe))
            {
                return;
            }

            _onTexturePathTransition?.Invoke(this, oldValue, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Representative silhouette tint color for the 2D thumbnail placeholder.
    /// </summary>
    public string SwatchHex { get; }

    /// <summary>
    ///     Optional absolute path to the resolved PNG sprite on disk.
    /// </summary>
    public string? ResolvedAbsolutePath => _resolvedAbsolutePath;

    /// <summary>
    ///     Decoded 2D sprite thumbnail when the backing PNG file exists on disk.
    /// </summary>
    public Bitmap? SpriteBitmap => _spriteBitmap;

    /// <summary>
    ///     Whether a real decoded sprite PNG bitmap is available for rendering.
    /// </summary>
    public bool HasSpriteBitmap => _spriteBitmap != null;

    /// <summary>
    ///     Short direction badge label for thumbnail framing.
    /// </summary>
    public string DirectionLabel => Direction.ToUpperInvariant();

    /// <summary>
    ///     Updates the resolved on-disk path and refreshes the decoded thumbnail bitmap.
    /// </summary>
    public void UpdateResolvedPath(string? resolvedAbsolutePath)
    {
        if (string.Equals(_resolvedAbsolutePath, resolvedAbsolutePath, StringComparison.Ordinal)
            && (_spriteBitmap != null || string.IsNullOrWhiteSpace(resolvedAbsolutePath)))
        {
            return;
        }

        _resolvedAbsolutePath = resolvedAbsolutePath;
        _spriteBitmap = TryLoadBitmap(resolvedAbsolutePath);
        OnPropertyChanged(nameof(ResolvedAbsolutePath));
        OnPropertyChanged(nameof(SpriteBitmap));
        OnPropertyChanged(nameof(HasSpriteBitmap));
    }

    /// <summary>
    ///     Restores the texture path during undo or redo replay without pushing another undo entry.
    /// </summary>
    public void ApplyTexturePathForReplay(string restoredPath, string? resolvedAbsolutePath)
    {
        SetProperty(ref _texturePath, restoredPath ?? string.Empty, nameof(TexturePath));
        UpdateResolvedPath(resolvedAbsolutePath);
    }

    private static Bitmap? TryLoadBitmap(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            return new Bitmap(filePath);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed loading body sprite thumbnail '{filePath}': {ex.Message}");
            return null;
        }
    }
}
