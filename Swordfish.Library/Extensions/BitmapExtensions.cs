using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Swordfish.Library.Extensions
{
    public static class BitmapExtensions
    {
        /// <summary>
        /// Resize this bitmap
        /// </summary>
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
            using (Graphics g = Graphics.FromImage(input))
            {
                g.InterpolationMode = interpolation;
                g.SmoothingMode = smoothing;
                g.PixelOffsetMode = offset;
                g.DrawImage(input, 0, 0, width, height);
            }
        }


        /// <summary>
        /// Set the gamma of this bitmap
        /// </summary>
        /// <param name="gamma">the gamma value as a scale, where 1 is no change</param>
        public static void SetGamma(this Bitmap input, float gamma = 1f)
        {
            ImageAttributes attributes = new ImageAttributes();
            attributes.SetGamma(1f/gamma);  //  Set gamma is a reversed scale, convert the input value

            Rectangle rect = new Rectangle(0, 0, input.Width, input.Height);

            Point[] points =
            {
                new Point(0, 0),
                new Point(input.Width, 0),
                new Point(0, input.Height),
            };

            using (Graphics g = Graphics.FromImage(input))
            {
                g.DrawImage(input, points, rect, GraphicsUnit.Pixel, attributes);
            }
        }
    }
}
