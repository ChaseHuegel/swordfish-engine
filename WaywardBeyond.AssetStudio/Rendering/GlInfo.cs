namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Reports active OpenGL vendor, renderer, and version metadata for diagnostics and the About dialog.</summary>
public readonly record struct GlInfo(
    string Vendor,
    string Renderer,
    string Version,
    bool IsOpenGlEs
);
