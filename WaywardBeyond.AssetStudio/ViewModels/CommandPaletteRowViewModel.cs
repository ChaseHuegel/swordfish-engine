using CommunityToolkit.Mvvm.ComponentModel;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Presentation row inside the command palette with keyboard selection and status styling flags.
/// </summary>
public sealed partial class CommandPaletteRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    ///     Initializes a command palette result row for an indexed entry.
    /// </summary>
    public CommandPaletteRowViewModel(CommandPaletteEntry entry, bool isSelected)
    {
        Entry = entry;
        _isSelected = isSelected;
    }

    /// <summary>
    ///     Underlying ranked command palette entry.
    /// </summary>
    public CommandPaletteEntry Entry { get; }

    /// <summary>
    ///     Whether the entry displays a provenance badge.
    /// </summary>
    public bool HasProvenance => Entry.Provenance != null;

    /// <summary>
    ///     Whether the entry's provenance is an override.
    /// </summary>
    public bool IsOverrideProvenance => Entry.Provenance == AssetProvenance.Override;

    /// <summary>
    ///     Whether the entry's provenance is pack-authored.
    /// </summary>
    public bool IsPackProvenance => Entry.Provenance == AssetProvenance.Pack;

    /// <summary>
    ///     Whether the resolution badge should use success styling.
    /// </summary>
    public bool IsResolutionSuccess => string.Equals(Entry.ResolutionLabel, "Resolved", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Whether the resolution badge should use warning styling.
    /// </summary>
    public bool IsResolutionWarning => Entry.ResolutionLabel.Contains("Dirty", StringComparison.OrdinalIgnoreCase)
        || Entry.ResolutionLabel.Contains("Case", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Whether the resolution badge should use danger styling.
    /// </summary>
    public bool IsResolutionDanger => Entry.ResolutionLabel.Contains("Error", StringComparison.OrdinalIgnoreCase)
        || Entry.ResolutionLabel.Contains("Missing", StringComparison.OrdinalIgnoreCase)
        || Entry.ResolutionLabel.Contains("Broken", StringComparison.OrdinalIgnoreCase);
}
