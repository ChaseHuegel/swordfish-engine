using System.Drawing;
using Silk.NET.Core;
using StbImageNext;
using Swordfish.Library.IO;
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global

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
        using Stream stream = path.Open();
        ImageResult image = DecodeRgba(stream);
        return new RawImage(image.Width, image.Height, image.Data);
    }

    /// <summary>
    ///     Loads an image buffer as a <see cref="RawImage"/>.
    /// </summary>
    /// <returns>The <see cref="RawImage"/> that was loaded.</returns>
    public static RawImage Load(byte[] buffer)
    {
        ImageResult image = ImageResult.FromMemory(buffer, ColorComponents.RedGreenBlueAlpha);
        return new RawImage(image.Width, image.Height, image.Data);
    }

    /// <summary>Decodes an image stream as RGBA, buffering non-seekable streams first.</summary>
    internal static ImageResult DecodeRgba(Stream stream)
    {
        if (stream.CanSeek)
        {
            return ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        }

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        memoryStream.Position = 0;
        return ImageResult.FromStream(memoryStream, ColorComponents.RedGreenBlueAlpha);
    }
}
