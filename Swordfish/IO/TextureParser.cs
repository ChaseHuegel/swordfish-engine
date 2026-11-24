using StbImageNext;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Util;

namespace Swordfish.IO
{
    internal class TextureParser : IFileParser<Texture>
    {
        public string[] SupportedExtensions { get; } =
        [
            ".png",
            ".bmp",
        ];

        object IFileParser.Parse(PathInfo file) => Parse(file);
        public Texture Parse(PathInfo file)
        {
            using Stream stream = file.Open();
            ImageResult image = Imaging.DecodeRgba(stream);
            image.VerticalFlip();

            string name = file.GetFileNameWithoutExtension();
            return new Texture(name, image.Data, image.Width, image.Height, true);
        }
    }
}
