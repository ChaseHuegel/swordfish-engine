using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.IO;

namespace Swordfish.IO
{
    internal class TextureArrayParser : IFileParser<TextureArray>
    {
        public string[] SupportedExtensions { get; } = new string[] {
            string.Empty
        };

        private readonly GLContext GLContext;
        private readonly IFileParser<Texture> TextureParser;

        public TextureArrayParser(GLContext glContext, IFileParser<Texture> textureParser)
        {
            GLContext = glContext;
            TextureParser = textureParser;
        }

        object IFileParser.Parse(IFileService fileService, IPath path) => Parse(fileService, path);
        public unsafe TextureArray Parse(IFileService fileService, IPath path)
        {
            string name = path.GetDirectoryName();

            IPath[] files = fileService.GetFiles(path);
            Texture[] textures = files.Select(file => TextureParser.Parse(fileService, file)).ToArray();

            return new TextureArray(name, textures.ToArray(), true);
        }
    }
}
