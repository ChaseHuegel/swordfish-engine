using System;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Components;

namespace Swordfish.Tests;

/// <summary>Context for the scroll input repro view.</summary>
internal readonly record struct ScrollReproState(bool ShowChoice, int Index);

/// <summary>View used to reproduce scroll area input behavior.</summary>
internal partial class ScrollReproView : UiViewBase
{
    /// <summary>Initializes the repro view.</summary>
    public ScrollReproView(ILocalization localization) : base(localization)
    {
    }

    /// <summary>Gets the scroll state.</summary>
    public ScrollAreaState Scroll { get; } = new();

    /// <summary>Gets the widget options.</summary>
    public string[] Options { get; } = ["Off", "On"];

    /// <summary>Gets or sets the plain click callback.</summary>
    public Action? PlainClicked { get; set; }

    /// <summary>Gets or sets the choice callback.</summary>
    public Action<int>? ChoiceChanged { get; set; }

    /// <summary>Gets the choice callback the widget receives.</summary>
    public Action<string, int>? ChoiceCallback => (_, index) => ChoiceChanged?.Invoke(index);

    partial void OnPlain() => PlainClicked?.Invoke();
}
