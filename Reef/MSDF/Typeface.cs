using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
    private readonly int _tabSize;
    private readonly Dictionary<int, Glyph> _glyphs = [];
    private readonly Glyph _unknownGlyph;

    public string ID { get; }

    public Typeface(string id, GlyphAtlas glyphAtlas, string atlasPath, int tabSize)
    {
        ID = id;
        _atlasPath = atlasPath;
        _atlas = glyphAtlas.atlas;
        _metrics = glyphAtlas.metrics;
        _tabSize = tabSize;
        
        for (var i = 0; i < glyphAtlas.glyphs.Length; i++)
        {
            Glyph glyph = glyphAtlas.glyphs[i];
            _glyphs[glyph.unicode] = glyph;
        }
        
        if (!_glyphs.TryGetValue(U_WHITE_SQUARE, out Glyph? unknownGlyph))
        {
            unknownGlyph = _glyphs.Count > 0 ? _glyphs.Values.First() : new Glyph(0, 0, PlaneBounds.Zero, AtlasBounds.Zero);
        }

        _unknownGlyph = unknownGlyph;
    }

    public AtlasInfo GetAtlasInfo()
    {
        return new AtlasInfo(_atlasPath);
    }
    
    public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length)
    {
        RichText parsed = RichText.Parse(text, start, length);
        return Measure(fontOptions, parsed.Text.AsSpan());
    }

    private TextConstraints Measure(FontOptions fontOptions, ReadOnlySpan<char> text)
    {
        float scale = fontOptions.Size / _metrics.emSize;
        int minWidthPx = MeasureSpanWidth(text, scale);
        var minHeightPx = (int)Math.Floor(_metrics.lineHeight * scale);
        return new TextConstraints(minWidthPx, minHeightPx, minWidthPx, minHeightPx);
    }

    private int MeasureSpanWidth(ReadOnlySpan<char> text, float scale)
    {
        var widthEm = 0d;
        for (var i = 0; i < text.Length; i++)
        {
            char c = text[i];
            Glyph glyph = _glyphs.GetValueOrDefault(c, _unknownGlyph);
            
            //  Tabs can represent multiple advances
            widthEm += c != '\t' ? glyph.advance : glyph.advance * _tabSize;
        }

        return (int)Math.Floor(widthEm * scale);
    }

    public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth)
    {
        RichText parsed = RichText.Parse(text, start, length);
        return Layout(fontOptions, parsed, maxWidth);
    }

    private TextLayout Layout(FontOptions fontOptions, RichText parsed, int maxWidth)
    {
        if (fontOptions.NoWrap)
        {
            maxWidth = int.MaxValue;
        }

        float scale = fontOptions.Size / _metrics.emSize;
        var glyphs = new List<GlyphLayout>(parsed.Text.Length);
        
        (string[] lines, int[] lineStarts) = Wrap(fontOptions, parsed.Text, maxWidth);

        var lineAdvances = new double[lines.Length];
        var bboxWidth = 0d;
        for (var i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            var lineAdvance = 0d;
            for (var n = 0; n < line.Length; n++)
            {
                char c = line[n];
                Glyph glyph = _glyphs.GetValueOrDefault(c, _unknownGlyph);
                lineAdvance += c != '\t' ? glyph.advance : glyph.advance * _tabSize;
            }

            lineAdvances[i] = lineAdvance;
            bboxWidth = Math.Max(bboxWidth, lineAdvance);
        }

        double baseline = _metrics.ascender > 0d ? _metrics.ascender : 1d;
        var bboxHeight = 0d;
        var yOffset = 0d;
        for (var i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            double xOffset = fontOptions.HorizontalAlignment switch
            {
                HorizontalAlignment.Center => (bboxWidth - lineAdvances[i]) * 0.5d,
                HorizontalAlignment.Right => bboxWidth - lineAdvances[i],
                _ => 0d,
            };

            for (var n = 0; n < line.Length; n++)
            {
                int charIndex = lineStarts[i] + n;
                char c = parsed.Text[charIndex];
                Glyph glyph = _glyphs.GetValueOrDefault(c, _unknownGlyph);
                Vector4? glyphColor = parsed.Color[charIndex];

                //  MSDF uses the bottom-left corner as the origin, offset top/bottom to shift the origin point to top-left.
                PlaneBounds planeBounds = glyph.planeBounds ?? PlaneBounds.Zero;
                var left = (int)Math.Floor((planeBounds.left + xOffset) * scale);
                var top = (int)Math.Floor((baseline - planeBounds.top + yOffset) * scale);
                var bottom = (int)Math.Ceiling((baseline - planeBounds.bottom + yOffset) * scale);
                
                int right;
                var isWhitespace = false;
                switch (c)
                {
                    //  Spaces should be sized to match one advance
                    case ' ':
                        right = left + (int)Math.Ceiling(glyph.advance * scale);
                        xOffset += glyph.advance;
                        isWhitespace = true;
                        break;
                    //  Tabs should be sized to match a number of advances equal to the tab size
                    case '\t':
                        right = left + (int)Math.Ceiling(glyph.advance * scale * _tabSize);
                        xOffset += glyph.advance * _tabSize;
                        isWhitespace = true;
                        break;
                    default:
                        right = (int)Math.Ceiling((planeBounds.right + xOffset) * scale);
                        xOffset += glyph.advance;
                        isWhitespace = glyph.atlasBounds == null;
                        break;
                }
                
                var bbox = new IntRect(left, top, right, bottom);
                
                //  MSDF uses the bottom-left corner as the origin, offset top/bottom to shift the origin point to top-left.
                AtlasBounds atlasBounds = glyph.atlasBounds ?? AtlasBounds.Zero;
                left = (int)Math.Floor(atlasBounds.left);
                top = (int)Math.Floor(_atlas.height - atlasBounds.top);
                right = (int)Math.Ceiling(atlasBounds.right);
                bottom = (int)Math.Ceiling(_atlas.height - atlasBounds.bottom);
                var uv = new IntRect(left, top, right, bottom);

                glyphs.Add(new GlyphLayout(bbox, uv, glyphColor, isWhitespace));
            }

            yOffset += _metrics.lineHeight;
            bboxHeight += _metrics.lineHeight;
        }

        var width = (int)Math.Ceiling(bboxWidth * scale);
        var height = (int)Math.Ceiling(bboxHeight * scale);
        var constraints = new TextConstraints(width, height, width, height);
        var lineHeight = (int)Math.Ceiling(_metrics.lineHeight * scale);
        return new TextLayout(constraints, glyphs.ToArray(), lines, lineHeight);
    }
    
    public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth)
    {
        RichText parsed = RichText.Parse(text, start, length);
        return Wrap(fontOptions, parsed.Text, maxWidth).Lines;
    }

    private (string[] Lines, int[] LineStarts) Wrap(FontOptions fontOptions, string text, int maxWidth)
    {
        if (fontOptions.NoWrap)
        {
            maxWidth = int.MaxValue;
        }

        if (text.Length == 0)
        {
            return ([], []);
        }

        float scale = fontOptions.Size / _metrics.emSize;
        ReadOnlySpan<char> span = text.AsSpan();
        var lines = new List<string>();
        var lineStarts = new List<int>();

        var paragraphStart = 0;
        while (paragraphStart <= text.Length)
        {
            int newlineOffset = span.Slice(paragraphStart).IndexOf('\n');
            int paragraphEnd = newlineOffset < 0 ? text.Length : paragraphStart + newlineOffset;

            if (paragraphStart == paragraphEnd)
            {
                if (newlineOffset >= 0 || paragraphStart > 0)
                {
                    lines.Add(string.Empty);
                    lineStarts.Add(paragraphStart);
                }
                paragraphStart = paragraphEnd + 1;
                continue;
            }

            int lineStart = paragraphStart;
            var lineLength = 0;
            int i = paragraphStart;

            while (i < paragraphEnd)
            {
                int wsStart = i;
                while (i < paragraphEnd && (span[i] == ' ' || span[i] == '\t'))
                {
                    i++;
                }
                int wsLength = i - wsStart;

                int wordStart = i;
                while (i < paragraphEnd && span[i] != ' ' && span[i] != '\t')
                {
                    i++;
                }
                int wordLength = i - wordStart;

                if (wordLength == 0)
                {
                    if (lineLength == 0)
                    {
                        lines.Add(text.Substring(wsStart, wsLength));
                        lineStarts.Add(wsStart);
                    }
                    else
                    {
                        int candidateLength = wsStart + wsLength - lineStart;
                        if (MeasureSpanWidth(span.Slice(lineStart, candidateLength), scale) <= maxWidth)
                        {
                            lineLength = candidateLength;
                        }
                    }
                    break;
                }

                if (lineLength > 0)
                {
                    int candidateLength = wordStart + wordLength - lineStart;
                    if (MeasureSpanWidth(span.Slice(lineStart, candidateLength), scale) <= maxWidth)
                    {
                        lineLength = candidateLength;
                        continue;
                    }

                    lines.Add(text.Substring(lineStart, lineLength));
                    lineStarts.Add(lineStart);
                    lineStart = wordStart;
                    lineLength = 0;
                }
                else if (wsStart == paragraphStart && wsLength > 0)
                {
                    int candidateLength = wordStart + wordLength - wsStart;
                    if (MeasureSpanWidth(span.Slice(wsStart, candidateLength), scale) <= maxWidth)
                    {
                        lineStart = wsStart;
                        lineLength = candidateLength;
                        continue;
                    }
                }

                while (wordLength > 1 && MeasureSpanWidth(span.Slice(wordStart, wordLength), scale) > maxWidth)
                {
                    var splitIndex = 1;
                    while (splitIndex < wordLength && MeasureSpanWidth(span.Slice(wordStart, splitIndex + 1), scale) <= maxWidth)
                    {
                        splitIndex++;
                    }

                    lines.Add(text.Substring(wordStart, splitIndex));
                    lineStarts.Add(wordStart);
                    wordStart += splitIndex;
                    wordLength -= splitIndex;
                }

                lineStart = wordStart;
                lineLength = wordLength;
            }

            if (lineLength > 0)
            {
                lines.Add(text.Substring(lineStart, lineLength));
                lineStarts.Add(lineStart);
            }

            if (newlineOffset < 0)
            {
                break;
            }

            paragraphStart = paragraphEnd + 1;
        }

        return (lines.ToArray(), lineStarts.ToArray());
    }
}
