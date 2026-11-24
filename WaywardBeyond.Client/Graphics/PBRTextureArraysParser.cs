using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Graphics;

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

        GenerateMissingTextures(albedoTextures, metallicTextures, ".m", r: 0, g: 0, b: 0, a: 255);
        GenerateMissingTextures(albedoTextures, smoothnessTextures, ".s", r: 0, g: 0, b: 0, a: 255);
        GenerateMissingTextures(albedoTextures, normalTextures, ".n", r: 0, g: 0, b: 255, a: 255);
        GenerateMissingTextures(albedoTextures, emissiveTextures, ".e", r: 0, g: 0, b: 0, a: 0);

        Texture[] sortedAlbedo = albedoTextures.OrderBy(texture => texture.Name, new NaturalComparer()).ToArray();
        Texture[] sortedMetallic = metallicTextures.OrderBy(texture => texture.Name, new NaturalComparer()).ToArray();
        Texture[] sortedSmoothness = smoothnessTextures.OrderBy(texture => texture.Name, new NaturalComparer()).ToArray();
        Texture[] sortedNormal = normalTextures.OrderBy(texture => texture.Name, new NaturalComparer()).ToArray();
        Texture[] sortedEmissive = emissiveTextures.OrderBy(texture => texture.Name, new NaturalComparer()).ToArray();

        int width = sortedAlbedo.Length > 0 ? sortedAlbedo[0].Width : 16;
        int height = sortedAlbedo.Length > 0 ? sortedAlbedo[0].Height : 16;

        Texture missingAlbedo = CreateMissingAlbedoTexture(width, height);
        Texture missingMetallic = CreateSolidTexture("missing.m", width, height, r: 0, g: 0, b: 0, a: 255);
        Texture missingSmoothness = CreateSolidTexture("missing.s", width, height, r: 0, g: 0, b: 0, a: 255);
        Texture missingNormal = CreateSolidTexture("missing.n", width, height, r: 0, g: 0, b: 255, a: 255);
        Texture missingEmissive = CreateSolidTexture("missing.e", width, height, r: 0, g: 0, b: 0, a: 0);

        Texture[] albedoTexturesArray = [missingAlbedo, .. sortedAlbedo];
        Texture[] metallicTexturesArray = [missingMetallic, .. sortedMetallic];
        Texture[] smoothnessTexturesArray = [missingSmoothness, .. sortedSmoothness];
        Texture[] normalTexturesArray = [missingNormal, .. sortedNormal];
        Texture[] emissiveTexturesArray = [missingEmissive, .. sortedEmissive];

        var albedoTextureArray = new TextureArray("bricks_albedo", albedoTexturesArray, mipmaps: true, ColorSpace.Srgb);
        var metallicTextureArray = new TextureArray("bricks_metallic", metallicTexturesArray, mipmaps: true, ColorSpace.Linear);
        var smoothnessTextureArray = new TextureArray("bricks_smoothness", smoothnessTexturesArray, mipmaps: true, ColorSpace.Linear);
        var normalTextureArray = new TextureArray("bricks_normal", normalTexturesArray, mipmaps: true, ColorSpace.Linear);
        var emissiveTextureArray = new TextureArray("bricks_emissive", emissiveTexturesArray, mipmaps: true, ColorSpace.Srgb);
        return new PBRTextureArrays(albedoTextureArray, metallicTextureArray, smoothnessTextureArray, normalTextureArray, emissiveTextureArray);
    }

    /// <summary>Creates a magenta and black checkerboard placeholder texture for missing or unresolved bricks.</summary>
    private static Texture CreateMissingAlbedoTexture(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        int blockWidth = Math.Max(width / 2, 1);
        int blockHeight = Math.Max(height / 2, 1);
        for (var y = 0; y < height; y++)
        {
            int rowOffset = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                bool magenta = (x / blockWidth + y / blockHeight) % 2 == 0;
                int offset = rowOffset + x * 4;
                pixels[offset] = magenta ? (byte)255 : (byte)0;
                pixels[offset + 1] = 0;
                pixels[offset + 2] = magenta ? (byte)255 : (byte)0;
                pixels[offset + 3] = 255;
            }
        }

        return new Texture("missing", pixels, width, height, mipmaps: true);
    }

    /// <summary>Creates a solid-color fallback texture layer.</summary>
    private static Texture CreateSolidTexture(string name, int width, int height, byte r, byte g, byte b, byte a)
    {
        byte[] pixels = CreateSolidPixels(width, height, r, g, b, a);
        return new Texture(name, pixels, width, height, mipmaps: true);
    }

    private static void GenerateMissingTextures(List<Texture> albedoTextures, List<Texture> textures, string suffix, byte r, byte g, byte b, byte a)
    {
        IEnumerable<Texture> missingTextures = albedoTextures.Except(textures, new TextureNameComparer(suffix));
        foreach (Texture albedoTexture in missingTextures)
        {
            byte[] pixels = CreateSolidPixels(albedoTexture.Width, albedoTexture.Height, r, g, b, a);
            var texture = new Texture(albedoTexture.Name + suffix, pixels, albedoTexture.Width, albedoTexture.Height, albedoTexture.Mipmaps);
            textures.Add(texture);
        }
    }

    private static byte[] CreateSolidPixels(int width, int height, byte r, byte g, byte b, byte a)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = a;
        }

        return pixels;
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