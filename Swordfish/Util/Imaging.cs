using System.Drawing;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using Swordfish.Library.IO;
using Image = SixLabors.ImageSharp.Image;

namespace Swordfish.Util;

public static class Imaging
{
    public static RawImage LoadIcon(IPath path)
    {
        //  ico isn't supported by ImageSharp so first load the image as a png
        using var stream = new MemoryStream();
        using var bitmap = new Bitmap(path.OriginalString);

        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        byte[] buffer = stream.ToArray();

        return Load(buffer);
    }

    public static RawImage Load(byte[] buffer)
    {
        var image = Image.Load<Rgba32>(buffer);
        return image.ToRawImage();
    }

    public static RawImage Load(IPath path)
    {
        var image = Image.Load<Rgba32>(path.OriginalString);
        return image.ToRawImage();
    }
}
