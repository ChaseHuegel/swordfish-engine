using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.IO;

namespace Swordfish.IO
{
    internal class TextureArrayParser : IFileParser<TexImage3D>
    {
        public string[] SupportedExtensions { get; } = new string[] {
            string.Empty
        };

        private readonly GLContext GLContext;

        public TextureArrayParser(GLContext glContext)
        {
            GLContext = glContext;
        }

        object IFileParser.Parse(IFileService fileService, IPath path) => Parse(fileService, path);
        public unsafe TexImage3D Parse(IFileService fileService, IPath path)
        {
            string name = path.GetDirectoryName();
            List<byte> pixels = new();
            uint width = 0, height = 0;

            IPath[] files = fileService.GetFiles(path);

            foreach (IPath file in files)
            {
                using Stream stream = fileService.Open(file);
                using StreamReader reader = new(stream);
                using Image<Rgba32> image = Image.Load<Rgba32>(stream);

                width = (uint)image.Width;
                height = (uint)image.Height;
                byte[] data = new byte[sizeof(Rgba32) * image.Width * image.Height];
                image.CopyPixelDataTo(data);
                pixels.AddRange(data);
            }

            return GLContext.CreateTexImage3D(name, pixels.ToArray(), width, height, (uint)files.Length);
        }
    }
}
