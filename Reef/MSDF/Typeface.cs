using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Reef.MSDF.Models;
using Reef.Text;
using Reef.UI;

namespace Reef.MSDF;

internal sealed class Typeface : ITypeface
{
    private const int U_WHITE_SQUARE = 9633;
    
    private readonly string _atlasPath;
    private readonly Atlas _atlas;
    private readonly Metrics _metrics;
    private readonly Dictionary<int, Glyph> _glyphs = [];
    private readonly Glyph _unknownGlyph;
    
    public string ID { get; }

    public Typeface(string id, GlyphAtlas glyphAtlas, string atlasPath)
    {
        ID = id;
        _atlasPath = atlasPath;
        _atlas = glyphAtlas.atlas;
        _metrics = glyphAtlas.metrics;
        
        for (var i = 0; i < glyphAtlas.glyphs.Length; i++)
        {
            Glyph glyph = glyphAtlas.glyphs[i];
            _glyphs.Add(glyph.unicode, glyph);
        }
        
        if (!_glyphs.TryGetValue(U_WHITE_SQUARE, out Glyph? unknownGlyph))
        {
            unknownGlyph = _glyphs.Values.First();
        }

        _unknownGlyph = unknownGlyph;
    }

    public AtlasInfo GetAtlasInfo()
    {
        return new AtlasInfo(_atlasPath);
    }
    
    public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length)
    {
        var widthEm = 0d;
        for (int i = start; i < start + length; i++)
        {
            char c = text[i];
            Glyph glyph = _glyphs.GetValueOrDefault(c, _unknownGlyph);
            widthEm += glyph.advance;
        }

        float scale = fontOptions.Size / _metrics.emSize;
        var minWidthPx = (int)Math.Round(widthEm * scale, MidpointRounding.AwayFromZero);
        var minHeightPx = (int)Math.Round(_metrics.lineHeight * scale);
        return new TextConstraints(minWidthPx, minHeightPx, minWidthPx, minHeightPx);
    }

    public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth)
    {
        float scale = fontOptions.Size / _metrics.emSize;
        var glyphs = new List<GlyphLayout>();
        
        string[] lines = Wrap(fontOptions, text, start, length, maxWidth);

        var bboxWidth = 0d;
        var bboxHeight = 0d;
        var yOffset = 0d;
        for (var i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            glyphs.Capacity += line.Length;

            var xOffset = 0d;
            for (var n = 0; n < line.Length; n++)
            {
                Glyph glyph = _glyphs.GetValueOrDefault(line[n], _unknownGlyph);

                //  MSDF uses the bottom-left corner as the origin, offset top/bottom to shift the origin point to top-left.
                PlaneBounds planeBounds = glyph.planeBounds ?? PlaneBounds.Zero;
                var left = (int)Math.Round((planeBounds.left + xOffset) * scale, MidpointRounding.AwayFromZero);
                var top = (int)Math.Round((1f - planeBounds.top + yOffset) * scale, MidpointRounding.AwayFromZero);
                var right = (int)Math.Round((planeBounds.right + xOffset) * scale, MidpointRounding.AwayFromZero);
                var bottom = (int)Math.Round((1f - planeBounds.bottom + yOffset) * scale, MidpointRounding.AwayFromZero);
                var bbox = new IntRect(left, top, right, bottom);
                
                //  MSDF uses the bottom-left corner as the origin, offset top/bottom to shift the origin point to top-left.
                AtlasBounds atlasBounds = glyph.atlasBounds ?? AtlasBounds.Zero;
                left = (int)Math.Round(atlasBounds.left, MidpointRounding.ToZero);
                top = (int)Math.Round(_atlas.height - atlasBounds.top, MidpointRounding.ToZero);
                right = (int)Math.Round(atlasBounds.right, MidpointRounding.ToZero);
                bottom = (int)Math.Round(_atlas.height - atlasBounds.bottom, MidpointRounding.ToZero);
                var uv = new IntRect(left, top, right, bottom);

                glyphs.Add(new GlyphLayout(bbox, uv));
                xOffset += glyph.advance;
            }

            yOffset += _metrics.lineHeight;
            bboxWidth = Math.Max(bboxWidth, xOffset);
            bboxHeight += _metrics.lineHeight;
        }

        var width = (int)Math.Round(bboxWidth * scale, MidpointRounding.AwayFromZero);
        var height = (int)Math.Round(bboxHeight * scale, MidpointRounding.AwayFromZero);
        var constraints = new TextConstraints(width, height, width, height);
        return new TextLayout(constraints, glyphs.ToArray());
    }
    
    public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth)
    {
        var lines = new List<string>();
        var wordBuilder = new StringBuilder();
        var lineBuilder = new StringBuilder();
        float currentLineWidth = 0;

        for (int i = start; i < start + length; i++)
        {
            char c = text[i];
            wordBuilder.Append(c);

            //  Search for the end of a word, or else the end of the text
            if (c != ' ' && i < start + length - 1)
            {
                continue;
            }

            var word = wordBuilder.ToString();
            TextConstraints wordMeasurement = Measure(fontOptions, word, start: 0, word.Length);
            float wordWidth = wordMeasurement.PreferredWidth;

            // If the word doesn't fit on the current line, commit the current line.
            if (currentLineWidth + wordWidth > maxWidth && lineBuilder.Length > 0)
            {
                lines.Add(lineBuilder.ToString().TrimEnd());
                lineBuilder.Clear();
                currentLineWidth = 0;
            }

            lineBuilder.Append(wordBuilder);
            currentLineWidth += wordWidth;
            wordBuilder.Clear();
        }

        // Flush any remaining line
        if (lineBuilder.Length > 0)
        {
            lines.Add(lineBuilder.ToString().TrimEnd());
        }

        return lines.ToArray();
    }
}