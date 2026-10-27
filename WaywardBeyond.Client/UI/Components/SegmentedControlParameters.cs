using System;

namespace WaywardBeyond.Client.UI.Components;

/// <summary>Parameters for the shared segmented choice control.</summary>
internal sealed record SegmentedControlParameters(
    string Id = "",
    string[] Options = null!,
    int Selected = 0,
    bool Enabled = true,
    Action<string, int>? OnSelect = null
);
