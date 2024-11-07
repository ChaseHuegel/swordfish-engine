using System.Drawing;
using Silk.NET.Core;
using SixLabors.ImageSharp.PixelFormats;
using Swordfish.Library.IO;

using Image = SixLabors.ImageSharp.Image;

namespace Swordfish.Util;

//  TODO create IFileParsers from this
public static class Imaging
{
    /// <inheritdoc cref="Load"/>
    /// <remarks>This is useful when trying to load an unsupported image type, such as ICO.</remarks>
    public static RawImage LoadAsPng(PathInfo path)
    {
        using var stream = new MemoryStream();
        using var bitmap = new Bitmap(path.Value);

        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        byte[] buffer = stream.ToArray();

        return Load(buffer);
    }

    /// <inheritdoc cref="Load"/>
    /// <remarks>This is useful when trying to load an unsupported image type, such as ICO.</remarks>
    public static RawImage LoadAsPng(Stream stream)
    {
        using (stream)
        {
            using var conversionStream = new MemoryStream();
            using var bitmap = new Bitmap(stream);

            bitmap.Save(conversionStream, System.Drawing.Imaging.ImageFormat.Png);
            byte[] buffer = conversionStream.ToArray();

            return Load(buffer);
        }
    }

    /// <summary>
    ///     Loads an image from disk as a <see cref="RawImage"/>.
    /// </summary>
    /// <param name="path">Path to load an image from.</param>
    /// <returns>The <see cref="RawImage"/> that was loaded.</returns>
    public static RawImage Load(PathInfo path)
    {
        var image = Image.Load<Rgba32>(path.Value);
        return image.ToRawImage();
    }

    /// <summary>
    ///     Loads an image buffer as a <see cref="RawImage"/>.
    /// </summary>
    /// <param name="path">Path to load an image from.</param>
    /// <returns>The <see cref="RawImage"/> that was loaded.</returns>
    public static RawImage Load(byte[] buffer)
    {
        var image = Image.Load<Rgba32>(buffer);
        return image.ToRawImage();
    }
}
