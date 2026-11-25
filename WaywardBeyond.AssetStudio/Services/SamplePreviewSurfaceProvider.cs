using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Default preview surface provider used for headless UI and fallback software previews.
/// </summary>
public sealed class SamplePreviewSurfaceProvider : IPreviewSurfaceProvider
{
    /// <inheritdoc/>
    public event Action? BackendInfoChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public string BackendLabel => OperatingSystem.IsWindows() ? "WGL · 330 core" : "GLX · 330 core (llvmpipe)";

    /// <inheritdoc/>
    public string VendorRendererLabel => OperatingSystem.IsWindows()
        ? "ANGLE / OpenGL 3.3 Core"
        : "Mesa / llvmpipe (OpenGL 4.5 Core · 330 core shader)";

    /// <inheritdoc/>
    public PreviewSurfaceMode ResolveSurfaceMode(AssetKind kind, string? shapeName, bool useItemWorldModel)
    {
        return kind switch
        {
            AssetKind.Brick when string.Equals(shapeName, "Any", StringComparison.OrdinalIgnoreCase) => PreviewSurfaceMode.BrickShapeGrid,
            AssetKind.Brick => PreviewSurfaceMode.SingleBrick,
            AssetKind.Item when useItemWorldModel => PreviewSurfaceMode.ItemWorldModel,
            AssetKind.Item => PreviewSurfaceMode.ItemViewModel,
            AssetKind.Paint => PreviewSurfaceMode.PaintSwatch,
            AssetKind.Body => PreviewSurfaceMode.BodySprite,
            AssetKind.Material => PreviewSurfaceMode.MaterialPreview,
            AssetKind.Blueprint => PreviewSurfaceMode.BlueprintVoxel,
            _ => PreviewSurfaceMode.DataDocument,
        };
    }

    /// <inheritdoc/>
    public void ReportLiveGlInfo(string vendor, string renderer, string version, bool isOpenGlEs)
    {
    }

    /// <inheritdoc/>
    public void ReportGlFallback(string reason)
    {
    }
}
