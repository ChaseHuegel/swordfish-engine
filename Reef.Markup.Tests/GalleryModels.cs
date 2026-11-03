using System.Numerics;
using Reef;
using Reef.Markup.TestComponents;

namespace Reef.Markup.Tests;

public sealed class GalleryColor
{
    public string Name { get; set; } = string.Empty;
    public Vector4 Value { get; set; }
}

public sealed class GalleryState
{
    public PixelTexture Texture { get; set; } = new(1, 1);
    public List<GalleryColor> Colors { get; } =
    [
        new GalleryColor { Name = "red", Value = new Vector4(1f, 0f, 0f, 1f) },
        new GalleryColor { Name = "green", Value = new Vector4(0f, 1f, 0f, 1f) },
        new GalleryColor { Name = "blue", Value = new Vector4(0f, 0f, 1f, 1f) },
    ];
    public PlayerInfo Player { get; } = new() { Name = "Ada", Score = 99 };
    public ModalState Modal { get; } = new() { Title = "Gallery" };
    public bool Disabled { get; set; }
}
