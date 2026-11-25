using System;
using System.Collections.Generic;
using WaywardBeyond.Meshing;

namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Holds decoded, vertically flipped RGBA8 block albedo layers and resolves bare layer names to layer indices.</summary>
public sealed class AlbedoTextureArrayData : ITextureLayerLookup
{
    private readonly Dictionary<string, int> _indices;

    /// <summary>Creates an immutable albedo texture array payload and lookup table.</summary>
    public AlbedoTextureArrayData(
        int width,
        int height,
        int layerCount,
        byte[] pixels,
        IReadOnlyList<string> layerNames
    ) {
        Width = width;
        Height = height;
        LayerCount = layerCount;
        Pixels = pixels;
        LayerNames = layerNames;

        _indices = new Dictionary<string, int>(layerNames.Count, StringComparer.Ordinal);
        for (var i = 0; i < layerNames.Count; i++)
        {
            _indices[layerNames[i]] = i;
        }
    }

    /// <summary>Layer width in pixels.</summary>
    public int Width { get; }

    /// <summary>Layer height in pixels.</summary>
    public int Height { get; }

    /// <summary>Total number of layers, including the synthesized layer 0 missing checkerboard.</summary>
    public int LayerCount { get; }

    /// <summary>Contiguous RGBA8 pixels ordered by layer index, vertically flipped for OpenGL UV origin.</summary>
    public byte[] Pixels { get; }

    /// <summary>Layer names ordered by texture-array index.</summary>
    public IReadOnlyList<string> LayerNames { get; }

    /// <inheritdoc/>
    public int IndexOf(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return -1;
        }

        return _indices.TryGetValue(name, out int index) ? index : -1;
    }
}
