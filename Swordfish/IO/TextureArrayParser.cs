using Swordfish.Graphics;
using Swordfish.Library.IO;

namespace Swordfish.IO
{
    internal class TextureArrayParser(in VirtualFileSystem vfs) : IFileParser<TextureArray>
    {
        private readonly VirtualFileSystem _vfs = vfs;
        private readonly TextureParser _textureParser = new();

        public string[] SupportedExtensions { get; } =
        [
            string.Empty,
        ];

        object IFileParser.Parse(PathInfo path) => Parse(path);
        public TextureArray Parse(PathInfo path)
        {
            string name = path.GetDirectoryName();

            PathInfo[] files = _vfs.GetFiles(path, SearchOption.AllDirectories);
            Texture[] textures = files.Select(_textureParser.Parse).ToArray();

            return new TextureArray(name, textures.ToArray(), true);
        }
    }
}
