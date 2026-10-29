using System;
using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Display data for the message modal.</summary>
internal readonly record struct MessageModalState(string Title, string Body);

/// <summary>Forwards message modal interactions to the hosting page.</summary>
internal partial class MessageModalView : UiViewBase
{
    /// <summary>Initializes the message modal view.</summary>
    public MessageModalView(ILocalization localization) : base(localization)
    {
    }

    /// <summary>Raised when the close action is clicked.</summary>
    public Action? CloseRequested { get; set; }

    partial void OnOk() => CloseRequested?.Invoke();
}
