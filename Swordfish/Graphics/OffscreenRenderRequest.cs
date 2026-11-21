using System.Numerics;

namespace Swordfish.Graphics;

/// <summary>Describes a single offscreen mesh render into a texture.</summary>
public readonly struct OffscreenRenderRequest(
    string name,
    Mesh mesh,
    Material material,
    RenderOptions renderOptions,
    Matrix4x4 model,
    Matrix4x4 view,
    Matrix4x4 projection,
    int width,
    int height,
    Vector4 clearColor = default,
    int samples = 4,
    bool mipmaps = true,
    TextureFilter filter = TextureFilter.Linear,
    ColorSpace colorSpace = ColorSpace.Srgb
) {
    public readonly string Name = name;
    public readonly Mesh Mesh = mesh;
    public readonly Material Material = material;
    public readonly RenderOptions RenderOptions = renderOptions;
    public readonly Matrix4x4 Model = model;
    public readonly Matrix4x4 View = view;
    public readonly Matrix4x4 Projection = projection;
    public readonly int Width = width;
    public readonly int Height = height;
    public readonly Vector4 ClearColor = clearColor;
    public readonly int Samples = samples;
    public readonly bool Mipmaps = mipmaps;
    public readonly TextureFilter Filter = filter;
    public readonly ColorSpace ColorSpace = colorSpace;
}
