using System;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Components;

namespace WaywardBeyond.Client.UI.Layers.Blueprints;

/// <summary>Passive Reef view for the Blueprint Library modal.</summary>
internal partial class BlueprintLibraryView : UiViewBase
{
    /// <summary>Initializes a new instance of the <see cref="BlueprintLibraryView"/> class.</summary>
    public BlueprintLibraryView(
        ILocalization localization,
        IInputService inputService,
        ScrollAreaState scroll
    ) : base(localization)
    {
        InputService = inputService;
        Scroll = scroll;
    }

    /// <summary>Gets the input service used by text fields.</summary>
    public IInputService InputService { get; }

    /// <summary>Gets the scroll state for the blueprint list.</summary>
    public ScrollAreaState Scroll { get; }

    /// <summary>Raised when the modal close action is triggered.</summary>
    public Action? CloseRequested { get; set; }

    /// <summary>Raised when saving the current selection or clipboard is requested.</summary>
    public Action? SaveRequested { get; set; }

    /// <summary>Raised when loading a blueprint into the clipboard is requested.</summary>
    public Action<string>? LoadRequested { get; set; }

    /// <summary>Raised when inline renaming starts for a blueprint.</summary>
    public Action<string>? StartRenameRequested { get; set; }

    /// <summary>Raised when inline renaming is confirmed for a blueprint.</summary>
    public Action<string>? CommitRenameRequested { get; set; }

    /// <summary>Raised when inline renaming is canceled.</summary>
    public Action? CancelRenameRequested { get; set; }

    /// <summary>Raised when the first delete click asks for confirmation.</summary>
    public Action<string>? RequestDeleteRequested { get; set; }

    /// <summary>Raised when deletion is confirmed for a blueprint.</summary>
    public Action<string>? ConfirmDeleteRequested { get; set; }

    /// <summary>Raised when pending deletion confirmation is canceled.</summary>
    public Action? CancelDeleteRequested { get; set; }

    /// <summary>Formats the dimensions and brick count line for a blueprint row.</summary>
    public string DimensionsText(in BlueprintListItemState item)
    {
        string baseText = $"{item.SizeX}x{item.SizeY}x{item.SizeZ} - {item.BrickCount} {L("ui.blueprint.bricksSuffix")}";
        return item.IsReadOnly ? $"{baseText} - {L("ui.blueprint.builtIn")}" : baseText;
    }

    partial void OnClose() => CloseRequested?.Invoke();

    partial void OnSave() => SaveRequested?.Invoke();

    partial void OnLoad(string id) => LoadRequested?.Invoke(id);

    partial void OnStartRename(string id) => StartRenameRequested?.Invoke(id);

    partial void OnCommitRename(string id) => CommitRenameRequested?.Invoke(id);

    partial void OnCancelRename() => CancelRenameRequested?.Invoke();

    partial void OnRequestDelete(string id) => RequestDeleteRequested?.Invoke(id);

    partial void OnConfirmDelete(string id) => ConfirmDeleteRequested?.Invoke(id);

    partial void OnCancelDelete() => CancelDeleteRequested?.Invoke();
}
