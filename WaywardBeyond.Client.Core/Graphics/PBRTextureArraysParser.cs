using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.Graphics;

internal sealed class PBRTextureArraysParser(in VirtualFileSystem vfs, in IFileParser<Texture> textureParser)
    : IFileParser<PBRTextureArrays>
{
    private readonly VirtualFileSystem _vfs = vfs;
    private readonly IFileParser<Texture> _textureParser = textureParser;

    public string[] SupportedExtensions { get; } =
    [
        string.Empty,
    ];

    object IFileParser.Parse(PathInfo path) => Parse(path);
    public PBRTextureArrays Parse(PathInfo path)
    {
        var diffuseFiles = new List<PathInfo>();
        var metallicFiles = new List<PathInfo>();
        var roughnessFiles = new List<PathInfo>();
        var normalFiles = new List<PathInfo>();
        var emissiveFiles = new List<PathInfo>();
        
        IEnumerable<PathInfo> files = _vfs.GetFiles(path, SearchOption.AllDirectories);
        foreach (PathInfo file in files)
        {
            if (IsMetallic(file))
            {
                metallicFiles.Add(file);
                continue;
            }
            
            if (IsRoughness(file))
            {
                roughnessFiles.Add(file);
                continue;
            }
            
            if (IsNormal(file))
            {
                normalFiles.Add(file);
                continue;
            }
            
            if (IsEmissive(file))
            {
                emissiveFiles.Add(file);
                continue;
            }
            
            diffuseFiles.Add(file);
        }
        
        List<Texture> diffuseTextures = diffuseFiles.Select(_textureParser.Parse).ToList();
        List<Texture> metallicTextures = metallicFiles.Select(_textureParser.Parse).ToList();
        List<Texture> roughnessTextures = roughnessFiles.Select(_textureParser.Parse).ToList();
        List<Texture> normalTextures = normalFiles.Select(_textureParser.Parse).ToList();
        List<Texture> emissiveTextures = emissiveFiles.Select(_textureParser.Parse).ToList();

        GenerateMissingTextures(diffuseTextures, metallicTextures, ".m", new Rgba32(r: 0, g: 0, b: 0, a: byte.MaxValue));
        GenerateMissingTextures(diffuseTextures, roughnessTextures, ".r", new Rgba32(r: 0, g: 0, b: 0, a: byte.MaxValue));
        GenerateMissingTextures(diffuseTextures, normalTextures, ".n", new Rgba32(r: 0, g: 0, b: byte.MaxValue, a: byte.MaxValue));
        GenerateMissingTextures(diffuseTextures, emissiveTextures, ".e", new Rgba32(r: 0, g: 0, b: 0, a: 0));

        Texture[] diffuseTexturesArray = diffuseTextures.OrderBy(texture => texture.Name, new NaturalComparer()).ToArray();
        Texture[] metallicTexturesArray = metallicTextures.OrderBy(texture => texture.Name, new NaturalComparer()).ToArray();
        Texture[] roughnessTexturesArray = roughnessTextures.OrderBy(texture => texture.Name, new NaturalComparer()).ToArray();
        Texture[] normalTexturesArray = normalTextures.OrderBy(texture => texture.Name, new NaturalComparer()).ToArray();
        Texture[] emissiveTexturesArray = emissiveTextures.OrderBy(texture => texture.Name, new NaturalComparer()).ToArray();
        
        var diffuseTextureArray = new TextureArray("bricks_diffuse", diffuseTexturesArray, mipmaps: true);
        var metallicTextureArray = new TextureArray("bricks_metallic", metallicTexturesArray, mipmaps: true);
        var roughnessTextureArray = new TextureArray("bricks_roughness", roughnessTexturesArray, mipmaps: true);
        var normalTextureArray = new TextureArray("bricks_normal", normalTexturesArray, mipmaps: true);
        var emissiveTextureArray = new TextureArray("bricks_emissive", emissiveTexturesArray, mipmaps: true);
        
        return new PBRTextureArrays(diffuseTextureArray, metallicTextureArray, roughnessTextureArray, normalTextureArray, emissiveTextureArray);
    }

    private static void GenerateMissingTextures(List<Texture> diffuseTextures, List<Texture> textures, string suffix, Rgba32 color)
    {
        IEnumerable<Texture> missingTextures = diffuseTextures.Except(textures, new TextureNameComparer(suffix));
        foreach (Texture diffuseTexture in missingTextures)
        {
            var pixels = new byte[diffuseTexture.Pixels.Length];
            
            var image = new Image<Rgba32>(diffuseTexture.Width, diffuseTexture.Height, color);
            image.CopyPixelDataTo(pixels);
            
            var texture = new Texture(diffuseTexture.Name + suffix, pixels, diffuseTexture.Width, diffuseTexture.Height, diffuseTexture.Mipmaps);
            textures.Add(texture);
        }
    }

    private static bool IsMetallic(PathInfo file) => file.Value.EndsWith(".m.png");
    private static bool IsRoughness(PathInfo file) => file.Value.EndsWith(".r.png");
    private static bool IsNormal(PathInfo file) => file.Value.EndsWith(".n.png");
    private static bool IsEmissive(PathInfo file) => file.Value.EndsWith(".e.png");
    
    private class TextureNameComparer(string suffix) : IEqualityComparer<Texture>
    {
        public bool Equals(Texture? x, Texture? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null)
            {
                return false;
            }

            if (y is null)
            {
                return false;
            }

            if (x.GetType() != y.GetType())
            {
                return false;
            }

            return x.Name.EndsWith(suffix) && x.Name.Contains(y.Name);
        }

        public int GetHashCode(Texture obj)
        {
            return 0;
        }
    }
}