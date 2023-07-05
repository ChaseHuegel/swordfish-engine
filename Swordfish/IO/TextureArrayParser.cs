using Swordfish.Graphics;
using Swordfish.Library.IO;

namespace Swordfish.IO
{
    internal class TextureArrayParser : IFileParser<TextureArray>
    {
        public string[] SupportedExtensions { get; } = new string[] {
            string.Empty
        };

        object IFileParser.Parse(IFileService fileService, IPath path) => Parse(fileService, path);
        public unsafe TextureArray Parse(IFileService fileService, IPath path)
        {
            string name = path.GetDirectoryName();

            IPath[] files = fileService.GetFiles(path);
            Texture[] textures = files.Select(fileService.Parse<Texture>).ToArray();

            return new TextureArray(name, textures.ToArray(), true);
        }
    }
}
