using System.Runtime.InteropServices;
using Silk.NET.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace Swordfish.Util;

public static class ImageSharpExtensions
{
    public unsafe static RawImage ToRawImage(this Image<Rgba32> image)
    {
        var memoryGroup = image.GetPixelMemoryGroup();
        Memory<byte> rawData = new byte[memoryGroup.TotalLength * sizeof(Rgba32)];
        Span<Rgba32> block = MemoryMarshal.Cast<byte, Rgba32>(rawData.Span);

        foreach (Memory<Rgba32> memory in memoryGroup)
        {
            memory.Span.CopyTo(block);
            block = block[memory.Length..];
        }

        return new RawImage(image.Width, image.Height, rawData);
    }
}
