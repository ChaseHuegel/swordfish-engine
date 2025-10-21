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
        var albedoFiles = new List<PathInfo>();
        var metallicFiles = new List<PathInfo>();
        var smoothnessFiles = new List<PathInfo>();
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
            
            if (IsSmoothness(file))
            {
                smoothnessFiles.Add(file);
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
            
            albedoFiles.Add(file);
        }
        
        List<Texture> albedoTextures = albedoFiles.Select(_textureParser.Parse).ToList();
        List<Texture> metallicTextures = metallicFiles.Select(_textureParser.Parse).ToList();
        List<Texture> smoothnessTextures = smoothnessFiles.Select(_textureParser.Parse).ToList();
        List<Texture> normalTextures = normalFiles.Select(_textureParser.Parse).ToList();
        List<Texture> emissiveTextures = emissiveFiles.Select(_textureParser.Parse).ToList();

        GenerateMissingTextures(albedoTextures, metallicTextures, ".m", new Rgba32(r: 0, g: 0, b: 0, a: 255));
        GenerateMissingTextures(albedoTextures, smoothnessTextures, ".s", new Rgba32(r: 0, g: 0, b: 0, a: 255));
        GenerateMissingTextures(albedoTextures, normalTextures, ".n", new Rgba32(r: 0, g: 0, b: 255, a: 255));
        GenerateMissingTextures(albedoTextures, emissiveTextures, ".e", new Rgba32(r: 0, g: 0, b: 0, a: 0));

        Texture[] albedoTexturesArray = albedoTextures.OrderBy(texture => texture.Name, new NaturalComparer()).ToArray();
        Texture[] metallicTexturesArray = metallicTextures.OrderBy(texture => texture.Name, new NaturalComparer()).ToArray();
        Texture[] smoothnessTexturesArray = smoothnessTextures.OrderBy(texture => texture.Name, new NaturalComparer()).ToArray();
        Texture[] normalTexturesArray = normalTextures.OrderBy(texture => texture.Name, new NaturalComparer()).ToArray();
        Texture[] emissiveTexturesArray = emissiveTextures.OrderBy(texture => texture.Name, new NaturalComparer()).ToArray();
        
        var albedoTextureArray = new TextureArray("bricks_albedo", albedoTexturesArray, mipmaps: true);
        var metallicTextureArray = new TextureArray("bricks_metallic", metallicTexturesArray, mipmaps: true);
        var smoothnessTextureArray = new TextureArray("bricks_smoothness", smoothnessTexturesArray, mipmaps: true);
        var normalTextureArray = new TextureArray("bricks_normal", normalTexturesArray, mipmaps: true);
        var emissiveTextureArray = new TextureArray("bricks_emissive", emissiveTexturesArray, mipmaps: true);
        
        return new PBRTextureArrays(albedoTextureArray, metallicTextureArray, smoothnessTextureArray, normalTextureArray, emissiveTextureArray);
    }

    private static void GenerateMissingTextures(List<Texture> albedoTextures, List<Texture> textures, string suffix, Rgba32 color)
    {
        IEnumerable<Texture> missingTextures = albedoTextures.Except(textures, new TextureNameComparer(suffix));
        foreach (Texture albedoTexture in missingTextures)
        {
            var pixels = new byte[albedoTexture.Pixels.Length];
            
            var image = new Image<Rgba32>(albedoTexture.Width, albedoTexture.Height, color);
            image.CopyPixelDataTo(pixels);
            
            var texture = new Texture(albedoTexture.Name + suffix, pixels, albedoTexture.Width, albedoTexture.Height, albedoTexture.Mipmaps);
            textures.Add(texture);
        }
    }

    private static bool IsMetallic(PathInfo file) => file.Value.EndsWith(".m.png");
    private static bool IsSmoothness(PathInfo file) => file.Value.EndsWith(".s.png");
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