using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Supplies preview backend metadata and descriptors for the studio inspector viewport.
/// </summary>
public interface IPreviewSurfaceProvider
{
    /// <summary>
    ///     Raised when the active OpenGL backend metadata changes after viewport initialization or fallback.
    /// </summary>
    event Action? BackendInfoChanged;

    /// <summary>
    ///     Short backend label displayed in the status bar.
    /// </summary>
    string BackendLabel { get; }

    /// <summary>
    ///     Vendor and renderer string reported in the about diagnostics dialog.
    /// </summary>
    string VendorRendererLabel { get; }

    /// <summary>
    ///     Determines the preview surface mode for an asset kind and shape configuration.
    /// </summary>
    PreviewSurfaceMode ResolveSurfaceMode(AssetKind kind, string? shapeName, bool useItemWorldModel);

    /// <summary>
    ///     Records live OpenGL vendor, renderer, and version strings reported by <c>GlViewport</c>.
    /// </summary>
    void ReportLiveGlInfo(string vendor, string renderer, string version, bool isOpenGlEs);

    /// <summary>
    ///     Records a non-fatal OpenGL initialization or rendering failure so fallback telemetry is displayed.
    /// </summary>
    void ReportGlFallback(string reason);
}
