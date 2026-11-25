using System;
using System.Collections.Generic;
using System.IO;
using Silk.NET.Core;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.Util;

namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Builds the natural-sorted block albedo texture array matching the game client layer order.</summary>
public static class PbrAlbedoArrayLoader
{
    private const int DEFAULT_TEXTURE_SIZE = 16;

    /// <summary>Loads block albedo textures across ordered <paramref name="assetRoots"/>, with later roots overriding earlier ones.</summary>
    public static Result<AlbedoTextureArrayData> LoadFromAssetRoots(IReadOnlyList<string> assetRoots)
    {
        if (assetRoots == null)
        {
            return Result<AlbedoTextureArrayData>.FromFailure("Asset root list is null.");
        }

        try
        {
            var filesByName = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var r = 0; r < assetRoots.Count; r++)
            {
                string root = assetRoots[r];
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                string blockDir = Path.Combine(root, "textures", "block");
                if (!Directory.Exists(blockDir))
                {
                    continue;
                }

                foreach (string filePath in Directory.EnumerateFiles(blockDir, "*", SearchOption.AllDirectories))
                {
                    if (!IsAlbedoTextureFile(filePath))
                    {
                        continue;
                    }

                    string layerName = Path.GetFileNameWithoutExtension(filePath);
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        filesByName[layerName] = filePath;
                    }
                }
            }

            if (filesByName.Count == 0)
            {
                return Result<AlbedoTextureArrayData>.FromSuccess(CreateMissingOnly(DEFAULT_TEXTURE_SIZE));
            }

            var sortedNames = new List<string>(filesByName.Keys);
            sortedNames.Sort(new NaturalComparer());

            var decoded = new List<(string Name, RawImage Image)>(sortedNames.Count);
            for (var i = 0; i < sortedNames.Count; i++)
            {
                string name = sortedNames[i];
                string path = filesByName[name];
                RawImage image = Imaging.Load(new PathInfo(path));
                decoded.Add((name, image));
            }

            int width = decoded[0].Image.Width > 0 ? decoded[0].Image.Width : DEFAULT_TEXTURE_SIZE;
            int height = decoded[0].Image.Height > 0 ? decoded[0].Image.Height : DEFAULT_TEXTURE_SIZE;
            int layerByteCount = width * height * 4;
            int totalLayers = decoded.Count + 1;
            var pixels = new byte[totalLayers * layerByteCount];
            var layerNames = new string[totalLayers];

            layerNames[0] = "missing";
            WriteMissingCheckerboard(pixels.AsSpan(0, layerByteCount), width, height);

            for (var i = 0; i < decoded.Count; i++)
            {
                (string name, RawImage img) = decoded[i];
                layerNames[i + 1] = name;
                Span<byte> dest = pixels.AsSpan((i + 1) * layerByteCount, layerByteCount);
                CopyFlippedRgba(img.Pixels.Span, img.Width, img.Height, dest, width, height);
            }

            return Result<AlbedoTextureArrayData>.FromSuccess(
                new AlbedoTextureArrayData(width, height, totalLayers, pixels, layerNames)
            );
        }
        catch (Exception ex)
        {
            return Result<AlbedoTextureArrayData>.FromFailure(ex);
        }
    }

    /// <summary>Creates a single-layer albedo array containing only the layer-0 magenta-and-black missing checkerboard.</summary>
    public static AlbedoTextureArrayData CreateMissingOnly(int size = DEFAULT_TEXTURE_SIZE)
    {
        int clamped = Math.Max(size, 2);
        var pixels = new byte[clamped * clamped * 4];
        WriteMissingCheckerboard(pixels, clamped, clamped);
        return new AlbedoTextureArrayData(clamped, clamped, layerCount: 1, pixels, ["missing"]);
    }

    /// <summary>Creates a single-layer albedo texture array from one image file on disk.</summary>
    public static Result<AlbedoTextureArrayData> LoadSingleTexture(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return Result<AlbedoTextureArrayData>.FromFailure($"Texture file not found: '{filePath}'.");
        }

        try
        {
            RawImage img = Imaging.Load(new PathInfo(filePath));
            int byteCount = img.Width * img.Height * 4;
            var pixels = new byte[byteCount];
            CopyFlippedRgba(img.Pixels.Span, img.Width, img.Height, pixels, img.Width, img.Height);
            string name = Path.GetFileNameWithoutExtension(filePath);
            return Result<AlbedoTextureArrayData>.FromSuccess(
                new AlbedoTextureArrayData(img.Width, img.Height, layerCount: 1, pixels, [name])
            );
        }
        catch (Exception ex)
        {
            return Result<AlbedoTextureArrayData>.FromFailure(ex);
        }
    }

    /// <summary>Returns true when <paramref name="filePath"/> is a block albedo texture rather than a PBR auxiliary map.</summary>
    public static bool IsAlbedoTextureFile(string filePath)
    {
        if (!filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            && !filePath.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string stem = Path.GetFileNameWithoutExtension(filePath);
        return !stem.EndsWith(".m", StringComparison.OrdinalIgnoreCase)
            && !stem.EndsWith(".s", StringComparison.OrdinalIgnoreCase)
            && !stem.EndsWith(".n", StringComparison.OrdinalIgnoreCase)
            && !stem.EndsWith(".e", StringComparison.OrdinalIgnoreCase)
            && !stem.EndsWith(".t", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteMissingCheckerboard(Span<byte> dest, int width, int height)
    {
        int blockWidth = Math.Max(width / 2, 1);
        int blockHeight = Math.Max(height / 2, 1);
        for (var y = 0; y < height; y++)
        {
            int rowOffset = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                bool magenta = ((x / blockWidth) + (y / blockHeight)) % 2 == 0;
                int offset = rowOffset + (x * 4);
                dest[offset] = magenta ? (byte)255 : (byte)0;
                dest[offset + 1] = 0;
                dest[offset + 2] = magenta ? (byte)255 : (byte)0;
                dest[offset + 3] = 255;
            }
        }
    }

    private static void CopyFlippedRgba(
        ReadOnlySpan<byte> src,
        int srcWidth,
        int srcHeight,
        Span<byte> dest,
        int dstWidth,
        int dstHeight
    ) {
        if (srcWidth == dstWidth && srcHeight == dstHeight)
        {
            int rowBytes = dstWidth * 4;
            for (var y = 0; y < dstHeight; y++)
            {
                int srcRow = dstHeight - 1 - y;
                src.Slice(srcRow * rowBytes, rowBytes).CopyTo(dest.Slice(y * rowBytes, rowBytes));
            }

            return;
        }

        for (var y = 0; y < dstHeight; y++)
        {
            int flippedY = dstHeight - 1 - y;
            int srcY = Math.Clamp((flippedY * srcHeight) / dstHeight, 0, srcHeight - 1);
            int dstRowOffset = y * dstWidth * 4;
            int srcRowOffset = srcY * srcWidth * 4;

            for (var x = 0; x < dstWidth; x++)
            {
                int srcX = Math.Clamp((x * srcWidth) / dstWidth, 0, srcWidth - 1);
                int srcIdx = srcRowOffset + (srcX * 4);
                int dstIdx = dstRowOffset + (x * 4);
                dest[dstIdx] = src[srcIdx];
                dest[dstIdx + 1] = src[srcIdx + 1];
                dest[dstIdx + 2] = src[srcIdx + 2];
                dest[dstIdx + 3] = src[srcIdx + 3];
            }
        }
    }
}
