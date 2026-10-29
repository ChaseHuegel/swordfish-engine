using System;
using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Display data for the connecting modal.</summary>
internal readonly record struct ConnectingModalState(string Title, string Target);

/// <summary>Forwards connecting modal interactions to the hosting page.</summary>
internal partial class ConnectingModalView : UiViewBase
{
    private const int SIGNAL_TRAVEL = 392;
    private const double SIGNAL_PERIOD_SECONDS = 1.8d;

    /// <summary>Initializes the connecting modal view.</summary>
    public ConnectingModalView(ILocalization localization) : base(localization)
    {
    }

    /// <summary>Raised when the cancel action is clicked.</summary>
    public Action? CancelRequested { get; set; }

    /// <summary>Returns the horizontal signal segment position.</summary>
    protected int SignalX(double time)
    {
        float progress = (float)(time / SIGNAL_PERIOD_SECONDS % 1d);
        return (int)(progress * SIGNAL_TRAVEL) - 56;
    }

    partial void OnCancel() => CancelRequested?.Invoke();
}
