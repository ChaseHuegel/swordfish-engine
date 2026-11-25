using System;
using System.Collections.Generic;
using System.IO;
using StbImageNext;
using Swordfish.Library.Util;

namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Decodes standalone 2D preview textures from disk with vertical flipping for OpenGL bottom-up UV space.</summary>
public static class StandaloneTextureLoader
{
    private const int DEFAULT_FALLBACK_SIZE = 16;

    /// <summary>Loads an image file from disk as RGBA8 pixels vertically flipped for OpenGL sampling.</summary>
    public static Result<StandaloneTextureData> LoadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return Result<StandaloneTextureData>.FromFailure($"Texture file not found: '{filePath}'.");
        }

        try
        {
            using FileStream stream = File.OpenRead(filePath);
            ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            if (image.Width <= 0 || image.Height <= 0 || image.Data == null || image.Data.Length < image.Width * image.Height * 4)
            {
                return Result<StandaloneTextureData>.FromFailure($"Decoded image '{filePath}' has invalid dimensions.");
            }

            // Match Swordfish.IO.TextureParser: flip top-down image rows to OpenGL bottom-up UV coordinates.
            image.VerticalFlip();
            return Result<StandaloneTextureData>.FromSuccess(new StandaloneTextureData(image.Width, image.Height, image.Data));
        }
        catch (Exception ex)
        {
            return Result<StandaloneTextureData>.FromFailure(ex);
        }
    }

    /// <summary>Resolves and loads a relative texture path across ordered workspace asset roots.</summary>
    public static Result<StandaloneTextureData> LoadFromAssetRoots(IReadOnlyList<string> assetRoots, string textureRelativePath)
    {
        string? resolved = ResolveTexturePath(assetRoots, textureRelativePath);
        if (resolved == null)
        {
            return Result<StandaloneTextureData>.FromFailure($"Texture '{textureRelativePath}' was not found in workspace asset roots.");
        }

        return LoadFromFile(resolved);
    }

    /// <summary>Resolves a relative texture path (such as <c>items/drill_uv.png</c>) to a winning file path on disk.</summary>
    public static string? ResolveTexturePath(IReadOnlyList<string> assetRoots, string? textureRelativePath)
    {
        if (assetRoots == null || assetRoots.Count == 0 || string.IsNullOrWhiteSpace(textureRelativePath))
        {
            return null;
        }

        string normalized = textureRelativePath.Trim().TrimStart('/', '\\').Replace('\\', '/');
        if (normalized.StartsWith("textures/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["textures/".Length..];
        }

        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        string subPath = Path.Combine(segments);
        for (int i = assetRoots.Count - 1; i >= 0; i--)
        {
            string root = assetRoots[i];
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            string texturesDir = Path.Combine(root, "textures");
            if (!Directory.Exists(texturesDir))
            {
                string nestedTexturesDir = Path.Combine(root, "assets", "textures");
                if (Directory.Exists(nestedTexturesDir))
                {
                    texturesDir = nestedTexturesDir;
                }
                else
                {
                    continue;
                }
            }

            string exactCandidate = Path.Combine(texturesDir, subPath);
            if (File.Exists(exactCandidate))
            {
                return exactCandidate;
            }

            string? caseInsensitive = FindCaseInsensitivePath(texturesDir, segments);
            if (caseInsensitive != null)
            {
                return caseInsensitive;
            }
        }

        return null;
    }

    /// <summary>Creates a magenta-and-black 2x2 checkerboard texture for missing standalone 2D textures.</summary>
    public static StandaloneTextureData CreateMissingCheckerboard(int size = DEFAULT_FALLBACK_SIZE)
    {
        int clamped = Math.Max(size, 2);
        var pixels = new byte[clamped * clamped * 4];
        int blockSize = Math.Max(clamped / 2, 1);

        for (var y = 0; y < clamped; y++)
        {
            int rowOffset = y * clamped * 4;
            for (var x = 0; x < clamped; x++)
            {
                bool magenta = ((x / blockSize) + (y / blockSize)) % 2 == 0;
                int offset = rowOffset + (x * 4);
                pixels[offset] = magenta ? (byte)255 : (byte)0;
                pixels[offset + 1] = 0;
                pixels[offset + 2] = magenta ? (byte)255 : (byte)0;
                pixels[offset + 3] = 255;
            }
        }

        return new StandaloneTextureData(clamped, clamped, pixels);
    }

    private static string? FindCaseInsensitivePath(string baseDirectory, string[] segments)
    {
        string current = baseDirectory;
        for (var i = 0; i < segments.Length; i++)
        {
            string target = segments[i];
            bool isLast = i == segments.Length - 1;
            string direct = Path.Combine(current, target);
            if (isLast)
            {
                if (File.Exists(direct))
                {
                    return direct;
                }

                if (!Directory.Exists(current))
                {
                    return null;
                }

                foreach (string file in Directory.EnumerateFiles(current))
                {
                    if (string.Equals(Path.GetFileName(file), target, StringComparison.OrdinalIgnoreCase))
                    {
                        return file;
                    }
                }

                return null;
            }

            if (Directory.Exists(direct))
            {
                current = direct;
                continue;
            }

            if (!Directory.Exists(current))
            {
                return null;
            }

            string? matchedDir = null;
            foreach (string dir in Directory.EnumerateDirectories(current))
            {
                if (string.Equals(Path.GetFileName(dir), target, StringComparison.OrdinalIgnoreCase))
                {
                    matchedDir = dir;
                    break;
                }
            }

            if (matchedDir == null)
            {
                return null;
            }

            current = matchedDir;
        }

        return null;
    }
}
