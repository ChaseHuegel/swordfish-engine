using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
        RichText parsed = RichText.Parse(text, start, length);
        return Measure(fontOptions, parsed.Text);
    }

    private TextConstraints Measure(FontOptions fontOptions, string text)
    {
        var widthEm = 0d;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            Glyph glyph = _glyphs.GetValueOrDefault(c, _unknownGlyph);
            
            //  Tabs can represent multiple advances
            widthEm += c != '\t' ? glyph.advance : glyph.advance * _tabSize;
        }

        float scale = fontOptions.Size / _metrics.emSize;
        var minWidthPx = (int)Math.Floor(widthEm * scale);
        var minHeightPx = (int)Math.Floor(_metrics.lineHeight * scale);
        return new TextConstraints(minWidthPx, minHeightPx, minWidthPx, minHeightPx);
    }

    public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth)
    {
        RichText parsed = RichText.Parse(text, start, length);
        return Layout(fontOptions, parsed, maxWidth);
    }

    private TextLayout Layout(FontOptions fontOptions, RichText parsed, int maxWidth)
    {
        float scale = fontOptions.Size / _metrics.emSize;
        var glyphs = new List<GlyphLayout>();
        
        (string[] lines, int[] lineStarts) = Wrap(fontOptions, parsed.Text, maxWidth);

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
                int charIndex = lineStarts[i] + n;
                Glyph glyph = _glyphs.GetValueOrDefault(parsed.Text[charIndex], _unknownGlyph);
                Vector4? glyphColor = parsed.Color[charIndex];

                //  MSDF uses the bottom-left corner as the origin, offset top/bottom to shift the origin point to top-left.
                PlaneBounds planeBounds = glyph.planeBounds ?? PlaneBounds.Zero;
                var left = (int)Math.Floor((planeBounds.left + xOffset) * scale);
                var top = (int)Math.Floor((1f - planeBounds.top + yOffset) * scale);
                var bottom = (int)Math.Ceiling((1f - planeBounds.bottom + yOffset) * scale);
                
                int right;
                switch (parsed.Text[charIndex])
                {
                    //  Spaces should be sized to match one advance
                    case ' ':
                        right = left + (int)Math.Ceiling(glyph.advance * scale);
                        xOffset += glyph.advance;
                        break;
                    //  Tabs should be sized to match a number of advances equal to the tab size
                    case '\t':
                        right = left + (int)Math.Ceiling(glyph.advance * scale * _tabSize);
                        xOffset += glyph.advance * _tabSize;
                        break;
                    default:
                        right = (int)Math.Ceiling((planeBounds.right + xOffset) * scale);
                        xOffset += glyph.advance;
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

                glyphs.Add(new GlyphLayout(bbox, uv, glyphColor));
            }

            yOffset += _metrics.lineHeight;
            bboxWidth = Math.Max(bboxWidth, xOffset);
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
        var lines = new List<string>();
        var lineStarts = new List<int>();
        var wordBuilder = new StringBuilder();
        var lineBuilder = new StringBuilder();
        float currentLineWidth = 0;
        var lineStart = 0;
        var wordStart = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            
            bool isNewline = c == '\n';
            bool isReturn = c == '\r';
            bool isWhitespace = c == ' ' || c == '\t';
            
            if (!isNewline && !isReturn)
            {
                if (wordBuilder.Length == 0)
                {
                    wordStart = i;
                }
                wordBuilder.Append(c);
            }

            //  Search for the end of a word (space or newline), or else the end of the text
            if (!isWhitespace &&!isNewline && i < text.Length - 1)
            {
                continue;
            }

            var word = wordBuilder.ToString();
            float wordWidth = Measure(fontOptions, word).PreferredWidth;

            bool overWidth = currentLineWidth + wordWidth > maxWidth;

            //  If the word doesn't fit on the current line or there is a newline,
            //  split the word into lines as necessary.
            if (overWidth || isNewline)
            {
                //  Commit the current line if this isn't a linebreak
                if (lineBuilder.Length > 0 && !isNewline)
                {
                    lines.Add(lineBuilder.ToString());
                    lineStarts.Add(lineStart);
                    lineBuilder.Clear();
                }
                
                //  While the word doesn't fit on the line, split it until it does
                while (Measure(fontOptions, wordBuilder.ToString()).PreferredWidth > maxWidth && wordBuilder.Length > 1)
                {
                    var splitIndex = 1;
                    while (splitIndex < wordBuilder.Length && Measure(fontOptions, wordBuilder.ToString(0, splitIndex + 1)).PreferredWidth <= maxWidth)
                    {
                        splitIndex++;
                    }
                    
                    //  Don't write empty words
                    if (!IsNullOrWhiteSpace(wordBuilder))
                    {
                        lines.Add(wordBuilder.ToString(0, splitIndex));
                        lineStarts.Add(wordStart);
                    }
                    
                    wordBuilder.Remove(0, splitIndex);
                    wordStart += splitIndex;
                }

                //  Flush any remaining word
                if (lineBuilder.Length == 0)
                {
                    lineStart = wordStart;
                }
                lineBuilder.Append(wordBuilder);
                wordBuilder.Clear();
                
                if (isNewline)
                {
                    lines.Add(lineBuilder.ToString());
                    lineStarts.Add(lineStart);
                    lineBuilder.Clear();
                    currentLineWidth = 0;
                }
                else
                {
                    currentLineWidth = Measure(fontOptions, lineBuilder.ToString()).PreferredWidth;
                }
            }
            else
            {
                if (lineBuilder.Length == 0)
                {
                    lineStart = wordStart;
                }
                currentLineWidth += wordWidth;
                lineBuilder.Append(wordBuilder);
                wordBuilder.Clear();
            }
        }
        
        //  Flush any remaining word
        if (wordBuilder.Length > 0)
        {
            lineBuilder.Append(wordBuilder);
        }

        // Flush any remaining line
        if (lineBuilder.Length > 0)
        {
            lines.Add(lineBuilder.ToString());
            lineStarts.Add(lineStart);
        }
        
        // If the text ends with a newline, tail with an empty line to represent it
        if (text.Length > 0 && text[text.Length - 1] == '\n')
        {
            lines.Add(string.Empty);
            lineStarts.Add(text.Length);
        }

        return (lines.ToArray(), lineStarts.ToArray());
    }

    private static bool IsNullOrWhiteSpace(StringBuilder stringBuilder)
    {
        for (var i = 0; i < stringBuilder.Length; i++)
        {
            if (!char.IsWhiteSpace(stringBuilder[i]))
            {
                return false;
            }
        }

        return true;
    }
}