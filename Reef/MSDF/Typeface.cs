using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Reef.MSDF.Models;
using Reef.Text;

namespace Reef.MSDF;

internal sealed class Typeface : ITypeface
{
    private const int U_WHITE_SQUARE = 9633;
    
    private readonly Atlas _atlas;
    private readonly Metrics _metrics;
    private readonly Dictionary<int, Glyph> _glyphs = [];
    private readonly Glyph _unknownGlyph;

    public Typeface(GlyphAtlas glyphAtlas)
    {
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
    
    public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length)
    {
        var widthEm = 0d;
        for (var i = 0; i < text.Length; i++)
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

    public IntRect[] Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth)
    {
        throw new NotImplementedException();
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

            TextConstraints wordMeasurement = Measure(fontOptions, wordBuilder.ToString(), start, length);
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