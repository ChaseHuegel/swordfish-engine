using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace Swordfish.IO;

internal class TextureCubemapParser(in VirtualFileSystem vfs) : IFileParser<TextureCubemap>
{
    private readonly VirtualFileSystem _vfs = vfs;
    private readonly TextureParser _textureParser = new();

    public string[] SupportedExtensions { get; } =
    [
        string.Empty,
    ];

    object IFileParser.Parse(PathInfo path) => Parse(path);
    public TextureCubemap Parse(PathInfo path)
    {
        string name = path.GetDirectoryName();

        PathInfo[] files = _vfs.GetFiles(path, SearchOption.AllDirectories);

        var textures = new Texture[6];
        for (var i = 0; i < files.Length; i++)
        {
            PathInfo file = files[i];
            
            if (file.GetFileNameWithoutExtension().Equals("right", StringComparison.InvariantCultureIgnoreCase))
            {
                textures[0] = _textureParser.Parse(file);
                continue;
            }
            
            if (file.GetFileNameWithoutExtension().Equals("left", StringComparison.InvariantCultureIgnoreCase))
            {
                textures[1] = _textureParser.Parse(file);
                continue;
            }
            
            if (file.GetFileNameWithoutExtension().Equals("top", StringComparison.InvariantCultureIgnoreCase))
            {
                textures[2] = _textureParser.Parse(file);
                continue;
            }
            
            if (file.GetFileNameWithoutExtension().Equals("bottom", StringComparison.InvariantCultureIgnoreCase))
            {
                textures[3] = _textureParser.Parse(file);
                continue;
            }
            
            if (file.GetFileNameWithoutExtension().Equals("front", StringComparison.InvariantCultureIgnoreCase))
            {
                textures[4] = _textureParser.Parse(file);
                continue;
            }
            
            if (file.GetFileNameWithoutExtension().Equals("back", StringComparison.InvariantCultureIgnoreCase))
            {
                textures[5] = _textureParser.Parse(file);
            }
        }

        for (var i = 0; i < textures.Length; i++)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (textures[i] == null)
            {
                textures[i] = new Texture(pixels: [], width: 0, height: 0, mipmaps: false);
            }
        }

        return new TextureCubemap(name, textures, mipmaps: false);
    }
}