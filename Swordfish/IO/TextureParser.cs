using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Swordfish.Graphics;
using Swordfish.Library.IO;

namespace Swordfish.IO
{
    internal class TextureParser : IFileParser<Texture>
    {
        public string[] SupportedExtensions { get; } =
        [
            ".png",
        ];

        object IFileParser.Parse(PathInfo file) => Parse(file);
        public unsafe Texture Parse(PathInfo file)
        {
            using Stream stream = file.Open();
            using StreamReader reader = new(stream);
            using Image<Rgba32> image = Image.Load<Rgba32>(stream);

            string name = file.GetFileNameWithoutExtension();

            var pixels = new byte[sizeof(Rgba32) * image.Width * image.Height];
            image.CopyPixelDataTo(pixels);

            return new Texture(name, pixels, image.Width, image.Height, true);
        }
    }
}
