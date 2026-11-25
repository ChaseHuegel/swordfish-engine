namespace WaywardBeyond.AssetTools.Settings;

/// <summary>
///     Persisted workbench panel dimensions and per-section collapse states.
/// </summary>
public sealed class PanelLayoutSettings
{
    /// <summary>
    ///     Gets or sets the width of the left navigation panel in logical pixels.
    /// </summary>
    public double LeftWidth { get; set; } = 280.0;

    /// <summary>
    ///     Gets or sets the width of the right preview and references panel in logical pixels.
    /// </summary>
    public double RightWidth { get; set; } = 320.0;

    /// <summary>
    ///     Gets or sets the height of the bottom validation panel in logical pixels.
    /// </summary>
    public double BottomHeight { get; set; } = 220.0;

    /// <summary>
    ///     Gets or sets a value indicating whether the bottom validation panel is expanded.
    /// </summary>
    public bool IsValidationOpen { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the right preview panel is expanded.
    /// </summary>
    public bool IsPreviewOpen { get; set; } = true;

    /// <summary>
    ///     Gets the map of form section identifiers to collapsed state.
    /// </summary>
    public Dictionary<string, bool> CollapsedSections { get; } = new(StringComparer.Ordinal);
}
