namespace WaywardBeyond.Client.UI.Components;

/// <summary>Parameters for the shared page frame.</summary>
internal sealed record PageParameters(
    int MaxWidth = 1280,
    int MaxHeight = 840,
    int MinWidth = 480,
    int MinHeight = 360,
    int InsetX = 60,
    int InsetY = 120,
    bool TopInset = false
);
