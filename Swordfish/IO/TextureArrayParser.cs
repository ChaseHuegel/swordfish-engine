using Swordfish.Graphics;
using Swordfish.Library.IO;

namespace Swordfish.IO
{
    internal class TextureArrayParser : IFileParser<TextureArray>
    {
        private readonly TextureParser _textureParser = new();

        public string[] SupportedExtensions { get; } =
        [
            string.Empty,
        ];

        object IFileParser.Parse(PathInfo path) => Parse(path);
        public TextureArray Parse(PathInfo path)
        {
            string name = path.GetDirectoryName();

            PathInfo[] files = path.GetFiles();
            Texture[] textures = files.Select(_textureParser.Parse).ToArray();

            return new TextureArray(name, textures.ToArray(), true);
        }
    }
}
