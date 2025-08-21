using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Reef.MSDF;
using Reef.MSDF.Models;
using Reef.MSDF.Serialization;

namespace Reef.Text;

using AtlasOutput = (string ImagePath, string JsonPath);

public sealed class TextEngine : ITextEngine
{
    private readonly ITypeface _defaultTypeface;
    private readonly Dictionary<string, ITypeface> _typefaces = [];

    public TextEngine(FontInfo[] fonts)
    {
        if (fonts.Length == 0)
        {
            throw new ArgumentException("At least one font must be provided.", nameof(fonts));
        }
        
        using var msdfAtlasGen = TempFile.CreateFromEmbeddedResource("Reef.Manifest.msdf-atlas-gen.exe");
        foreach (FontInfo fontInfo in fonts)
        {
            GenerateMSDF(msdfAtlasGen.Path, fontInfo, out AtlasOutput output);

            GlyphAtlas atlas = GlyphAtlasParser.Parse(output.JsonPath);
            var typeface = new Typeface(atlas, output.ImagePath);
            
            _typefaces.Add(fontInfo.ID, typeface);
        }
        
        _defaultTypeface = _typefaces.Values.First();
    }

    public bool TryGetTypeface(FontInfo fontInfo, out ITypeface? typeface)
    {
        return _typefaces.TryGetValue(fontInfo.ID, out typeface);
    }

    public TextConstraints Measure(FontOptions fontOptions, string text) => Measure(fontOptions, text, 0, text.Length);

    public TextLayout Layout(FontOptions fontOptions, string text) => Layout(fontOptions, text, 0, text.Length, int.MaxValue);

    public TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => Layout(fontOptions, text, 0, text.Length, maxWidth);

    public TextLayout Layout(FontOptions fontOptions, string text, int start, int length) => Layout(fontOptions, text, start, length, int.MaxValue);

    public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => Wrap(fontOptions, text, 0, text.Length, maxWidth);

    public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length)
    {
        if (fontOptions.ID != null && _typefaces.TryGetValue(fontOptions.ID, out ITypeface? typeface))
        {
            return typeface.Measure(fontOptions, text, start, length);
        }
        
        return _defaultTypeface.Measure(fontOptions, text, start, length);
    }

    public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth)
    {
        if (fontOptions.ID != null && _typefaces.TryGetValue(fontOptions.ID, out ITypeface? typeface))
        {
            return typeface.Layout(fontOptions, text, start, length, maxWidth);
        }
        
        return _defaultTypeface.Layout(fontOptions, text, start, length, maxWidth);
    }
    
    public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth)
    {
        if (fontOptions.ID != null && _typefaces.TryGetValue(fontOptions.ID, out ITypeface? typeface))
        {
            return typeface.Wrap(fontOptions, text, start, length, maxWidth);
        }
        
        return _defaultTypeface.Wrap(fontOptions, text, start, length, maxWidth);
    }
    
    private static void GenerateMSDF(string processPath, FontInfo fontInfo, out AtlasOutput output)
    {
        string fontFolder = Path.GetDirectoryName(fontInfo.Path)!;
        string imageDestination = Path.Combine(fontFolder, fontInfo.ID + ".bmp");
        string jsonDestination = Path.Combine(fontFolder, fontInfo.ID + ".json");
        output = new AtlasOutput(imageDestination, jsonDestination);

        if (File.Exists(imageDestination) && File.Exists(jsonDestination))
        {
            //  This font has already been generated at some point, don't re-generate it.
            return;
        }
        
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Runtime msdf-atlas-gen is only available on Windows. Atlas images and json files must be pre-generated for other platforms.");
        }
        
        var processStartInfo = new ProcessStartInfo
        {
            FileName = processPath,
            Arguments = $"-font \"{fontInfo.Path}\" -imageout \"{imageDestination}\" -json \"{jsonDestination}\" -chars \"[0x0000, 0xffff]\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        
        Process? process = Process.Start(processStartInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start the msdf-atlas-gen process.");
        }
        process.WaitForExit();

        if (!File.Exists(imageDestination) || !File.Exists(jsonDestination))
        {
            throw new InvalidOperationException($"Failed to generate the atlas for font \"{fontInfo.ID}\" at \"{fontInfo.Path}\".");
        }
    }
}