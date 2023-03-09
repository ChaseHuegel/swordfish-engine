using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.IO;

namespace Swordfish.IO
{
    internal class TextureParser : IFileParser<TexImage2D>
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
        public unsafe TexImage2D Parse(IFileService fileService, IPath file)
        {
            using Stream stream = fileService.Read(file);
            using StreamReader reader = new(stream);
            using Image<Rgba32> image = Image.Load<Rgba32>(stream);

            string name = file.GetFileNameWithoutExtension();

            byte[] pixels = new byte[sizeof(Rgba32) * image.Width * image.Height];
            image.CopyPixelDataTo(pixels);

            return GLContext.CreateTexImage2D(name, pixels, (uint)image.Width, (uint)image.Height);
        }
    }
}
