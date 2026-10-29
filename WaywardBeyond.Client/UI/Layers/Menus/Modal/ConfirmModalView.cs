using System;
using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Display data for the confirmation modal.</summary>
internal readonly record struct ConfirmModalState(
    string Title,
    string Body,
    string ConfirmLabel,
    string CancelLabel
);

/// <summary>Forwards confirmation modal interactions to the hosting page.</summary>
internal partial class ConfirmModalView : UiViewBase
{
    /// <summary>Initializes the confirmation modal view.</summary>
    public ConfirmModalView(ILocalization localization) : base(localization)
    {
    }

    /// <summary>Raised when the confirm action is clicked.</summary>
    public Action? ConfirmRequested { get; set; }

    /// <summary>Raised when the cancel action is clicked.</summary>
    public Action? CancelRequested { get; set; }

    partial void OnConfirm() => ConfirmRequested?.Invoke();

    partial void OnCancel() => CancelRequested?.Invoke();
}
