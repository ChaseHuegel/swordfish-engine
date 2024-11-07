using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Extensions;

// ReSharper disable once UnusedType.Global
public static class BitmapExtensions
{
    /// <summary>
    /// Resize this bitmap
    /// </summary>
    /// <param name="input">the bitmap to resize</param>
    /// <param name="width">new width in pixels</param>
    /// <param name="height">new height in pixels</param>
    /// <param name="interpolation">interpolation mode</param>
    /// <param name="smoothing">smoothing mode</param>
    /// <param name="offset">offset mode</param>
    public static void Resize(this Bitmap input,
        int width, int height,
        InterpolationMode interpolation = InterpolationMode.NearestNeighbor,
        SmoothingMode smoothing = SmoothingMode.None,
        PixelOffsetMode offset = PixelOffsetMode.Half)
    {
        using Graphics graphics = Graphics.FromImage(input);
        graphics.InterpolationMode = interpolation;
        graphics.SmoothingMode = smoothing;
        graphics.PixelOffsetMode = offset;
        graphics.DrawImage(input, 0, 0, width, height);
    }


    /// <summary>
    /// Set the gamma of this bitmap
    /// </summary>
    /// <param name="gamma">the gamma value as a scale, where 1 is no change</param>
    public static void SetGamma(this Bitmap input, float gamma = 1f)
    {
        var attributes = new ImageAttributes();
        attributes.SetGamma(1f / gamma);  //  Set gamma is a reversed scale, convert the input value

        var rect = new Rectangle(0, 0, input.Width, input.Height);

        Point[] points =
        [
            new(0, 0),
            new(input.Width, 0),
            new(0, input.Height),
        ];

        using Graphics graphics = Graphics.FromImage(input);
        graphics.DrawImage(input, points, rect, GraphicsUnit.Pixel, attributes);
    }
}