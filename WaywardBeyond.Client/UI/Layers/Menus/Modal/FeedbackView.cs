using System;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Forwards feedback modal interactions to the hosting page.</summary>
internal partial class FeedbackView : UiViewBase
{
    /// <summary>Initializes the feedback view.</summary>
    public FeedbackView(ILocalization localization, IInputService inputService) : base(localization)
    {
        InputService = inputService;
    }

    /// <summary>The input service used by the text fields.</summary>
    public IInputService InputService { get; }

    /// <summary>Raised when the back action is clicked.</summary>
    public Action? BackRequested { get; set; }

    /// <summary>Raised when the submit action is clicked.</summary>
    public Action? SubmitRequested { get; set; }

    /// <summary>Returns the description field height for the current window.</summary>
    protected static int DescriptionHeight(int windowHeight)
    {
        return Math.Clamp(windowHeight - 460, 160, 320);
    }

    partial void OnBack() => BackRequested?.Invoke();

    partial void OnSubmit() => SubmitRequested?.Invoke();
}
