using System;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Forwards add-favorite interactions to the hosting page.</summary>
internal partial class AddFavoriteView : UiViewBase
{
    /// <summary>Initializes the add-favorite view.</summary>
    public AddFavoriteView(ILocalization localization, IInputService inputService) : base(localization)
    {
        InputService = inputService;
    }

    /// <summary>The input service used by the text fields.</summary>
    public IInputService InputService { get; }

    /// <summary>Raised when the cancel action is clicked.</summary>
    public Action? CancelRequested { get; set; }

    /// <summary>Raised when the submit action is clicked.</summary>
    public Action? SubmitRequested { get; set; }

    partial void OnCancel() => CancelRequested?.Invoke();

    partial void OnSubmit() => SubmitRequested?.Invoke();
}
