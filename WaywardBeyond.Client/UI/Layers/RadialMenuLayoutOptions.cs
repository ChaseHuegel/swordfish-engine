using System;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Configuration for <see cref="RadialMenuLayout"/>.</summary>
internal readonly record struct RadialMenuLayoutOptions(
    float NodeSize,
    float MinGap,
    float InnerRadius,
    float CanvasMargin,
    int DefaultHitSize,
    float? MaxRadius
) {
    public static readonly RadialMenuLayoutOptions Default = new(
        NodeSize: 48f,
        MinGap: 4f,
        InnerRadius: 72f,
        CanvasMargin: 24f,
        DefaultHitSize: 88,
        MaxRadius: null
    );

    /// <summary>Creates default radial layout options.</summary>
    public RadialMenuLayoutOptions() : this(48f, 4f, 72f, 24f, 88, null) { }

    /// <summary>
    /// Minimum center-to-center spacing that guarantees square nodes never touch corners at any angle
    /// (bounding-circle diameter <c>NodeSize * sqrt(2)</c> plus <see cref="MinGap"/>).
    /// </summary>
    public float NodePitch => MathF.Ceiling(NodeSize * 1.41421356f) + MinGap;
}