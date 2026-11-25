using System.Collections.Generic;
using WaywardBeyond.Client.UI;

namespace WaywardBeyond.Client.UI.Layers.Blueprints;

/// <summary>Dynamic UI state for the Blueprint Library modal.</summary>
internal sealed class BlueprintLibraryState
{
    /// <summary>Search filter input state.</summary>
    public TextBoxState Search;

    /// <summary>Input state for saving the active selection or clipboard as a new blueprint.</summary>
    public TextBoxState SaveName;

    /// <summary>Input state for inline blueprint renaming.</summary>
    public TextBoxState RenameText;

    /// <summary>Initializes a new instance of the <see cref="BlueprintLibraryState"/> class.</summary>
    public BlueprintLibraryState(TextBoxState search, TextBoxState saveName, TextBoxState renameText)
    {
        Search = search;
        SaveName = saveName;
        RenameText = renameText;
    }

    /// <summary>Gets or sets whether a selection or clipboard region is currently available to save.</summary>
    public bool CanSave { get; set; }

    /// <summary>Gets or sets whether a save request is currently executing.</summary>
    public bool IsSaving { get; set; }

    /// <summary>Gets or sets the active localization error key, or empty when valid.</summary>
    public string ErrorKey { get; set; } = string.Empty;

    /// <summary>Gets whether an error message is currently shown.</summary>
    public bool HasError => ErrorKey.Length > 0;

    /// <summary>Gets or sets the filtered blueprint items displayed in the library list.</summary>
    public IReadOnlyList<BlueprintListItemState> Items { get; set; } = [];
}
