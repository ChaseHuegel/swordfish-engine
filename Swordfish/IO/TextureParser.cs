using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.IO;

namespace Swordfish.IO
{
    internal class TextureParser : IFileParser<Texture>
    {
        public string[] SupportedExtensions { get; } = new string[] {
            ".png"
        };

        private readonly GLContext GLContext;

        public TextureParser(GLContext glContext)
        {
            GLContext = glContext;
        }

        object IFileParser.Parse(IFileService fileService, IPath file) => Parse(fileService, file);
        public unsafe Texture Parse(IFileService fileService, IPath file)
        {
            using Stream stream = fileService.Open(file);
            using StreamReader reader = new(stream);
            using Image<Rgba32> image = Image.Load<Rgba32>(stream);

            string name = file.GetFileNameWithoutExtension();

            byte[] pixels = new byte[sizeof(Rgba32) * image.Width * image.Height];
            image.CopyPixelDataTo(pixels);

            return new Texture(name, pixels, image.Width, image.Height, true);
        }
    }
}
