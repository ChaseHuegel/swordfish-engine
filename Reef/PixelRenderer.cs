using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using Reef.Text;
using Reef.UI;

namespace Reef;

public sealed class PixelRenderer
{
    private readonly int _width;
    private readonly int _height;
    private readonly PixelTexture _renderTexture;
    private readonly ITextEngine _textEngine;
    private readonly IDictionary<string, PixelFontSDF> _fonts;
    
    public PixelRenderer(int width, int height, ITextEngine textEngine, IDictionary<string, PixelFontSDF> fonts)
    {
        _width = width;
        _height = height;
        _textEngine = textEngine;
        _fonts = fonts;
        _renderTexture = new PixelTexture(width, height);
    }
    
    public ReadOnlyCollection<Vector4> GetPixels()
    {
        return _renderTexture.GetPixels();
    }
    
    public void Render(List<RenderCommand<PixelTexture>> renderCommands)
    {
        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            _renderTexture.SetPixel(x, y, Vector4.Zero);
        }
        
        foreach (RenderCommand<PixelTexture> renderCommand in renderCommands)
        {
            Draw(renderCommand);
        }
    }
    
    private void Draw(RenderCommand<PixelTexture> renderCommand)
    {
        Vector4 color = renderCommand.Color;
        Vector4 backgroundColor = renderCommand.BackgroundColor;
        
        //  Render rects.
        //  IntRect right and bottom bounds are exclusive, matching IntRect.Contains.
        for (int y = renderCommand.Rect.Top; y < renderCommand.Rect.Bottom; y++)
        for (int x = renderCommand.Rect.Left; x < renderCommand.Rect.Right; x++)
        {
            //  Skip pixels that are outside the clip rect
            if (!renderCommand.ClipRect.Contains(x, y))
            {
                continue;
            }
            
            //  Skip pixels that are outside the corner radii
            if (IsOutsideCornerRadius(renderCommand, x, y))
            {
                continue;
            }

            //  Skip pixels that are outside the render target
            if (x < 0 || y < 0 || x >= _width || y >= _height)
            {
                continue;
            }

            Vector4 currentColor = _renderTexture.GetPixel(x, y);

            //  Render a textured rect
            if (renderCommand.RendererData != null)
            {
                var u = Math.Clamp((int)RangeToRange(x, renderCommand.Rect.Left, renderCommand.Rect.Right, 0, renderCommand.RendererData.Width), 0, renderCommand.RendererData.Width - 1);
                var v = Math.Clamp((int)RangeToRange(y, renderCommand.Rect.Top, renderCommand.Rect.Bottom, 0, renderCommand.RendererData.Height), 0, renderCommand.RendererData.Height - 1);
                Vector4 sample = renderCommand.RendererData.GetPixel(u, v);
                sample = MultiplyBlend(sample, color);
                //  Blend in any texture background color
                sample = AlphaBlend(backgroundColor, sample);
                
                _renderTexture.SetPixel(x, y, AlphaBlend(currentColor, sample));
            }
            //  Render a color rect
            else
            {
                //  If text is present, treat this as a background.
                Vector4 rectColor = renderCommand.Text == null ? color : backgroundColor;
                _renderTexture.SetPixel(x, y, AlphaBlend(currentColor, rectColor));
            }
        }

        DrawBorder(renderCommand);
        DrawCornerCaps(renderCommand);
        
        if (renderCommand.Text == null)
        {
            return;
        }

        //  Render text
        TextLayout textLayout = renderCommand.TextLayout ?? _textEngine.Layout(renderCommand.FontOptions, renderCommand.Text, renderCommand.Rect.Size.X);
        
        string? fontID = renderCommand.FontOptions.ID;
        if (fontID == null || !_fonts.TryGetValue(fontID, out PixelFontSDF font))
        {
            font = _fonts.Values.First();
        }
        
        for (var i = 0; i < textLayout.Glyphs.Length; i++)
        {
            GlyphLayout glyph = textLayout.Glyphs[i];
            if (glyph.IsWhitespace)
            {
                continue;
            }
                
            var bbox = new IntRect(
                renderCommand.Rect.Left + glyph.BBOX.Left,
                renderCommand.Rect.Top + glyph.BBOX.Top,
                renderCommand.Rect.Left + glyph.BBOX.Right,
                renderCommand.Rect.Top + glyph.BBOX.Bottom
            );

            DrawCharacter(bbox, renderCommand.ClipRect, glyph.UV, glyph.ResolveColor(color), font);
        }
    }
    
    private void DrawBorder(RenderCommand<PixelTexture> renderCommand)
    {
        BorderStyle border = renderCommand.Border;
        IntRect rect = renderCommand.Rect;
        int thickness = Math.Min(border.Thickness, Math.Min(rect.Size.X, rect.Size.Y) / 2);

        if (thickness <= 0 || border.Color.W <= 0f)
        {
            return;
        }

        DrawSolidRect(renderCommand, new IntRect(rect.Left, rect.Top, rect.Right, rect.Top + thickness), border.Color);
        DrawSolidRect(renderCommand, new IntRect(rect.Left, rect.Bottom - thickness, rect.Right, rect.Bottom), border.Color);
        DrawSolidRect(renderCommand, new IntRect(rect.Left, rect.Top + thickness, rect.Left + thickness, rect.Bottom - thickness), border.Color);
        DrawSolidRect(renderCommand, new IntRect(rect.Right - thickness, rect.Top + thickness, rect.Right, rect.Bottom - thickness), border.Color);
    }

    private void DrawCornerCaps(RenderCommand<PixelTexture> renderCommand)
    {
        CornerCapStyle caps = renderCommand.CornerCaps;
        IntRect rect = renderCommand.Rect;
        int maxLength = Math.Min(rect.Size.X, rect.Size.Y) / 2;
        int length = Math.Min(caps.Length, maxLength);
        int thickness = Math.Min(caps.Thickness, length);

        if (length <= 0 || thickness <= 0 || caps.Color.W <= 0f)
        {
            return;
        }

        //  Top left
        DrawSolidRect(renderCommand, new IntRect(rect.Left, rect.Top, rect.Left + length, rect.Top + thickness), caps.Color);
        DrawSolidRect(renderCommand, new IntRect(rect.Left, rect.Top + thickness, rect.Left + thickness, rect.Top + length), caps.Color);

        //  Top right
        DrawSolidRect(renderCommand, new IntRect(rect.Right - length, rect.Top, rect.Right, rect.Top + thickness), caps.Color);
        DrawSolidRect(renderCommand, new IntRect(rect.Right - thickness, rect.Top + thickness, rect.Right, rect.Top + length), caps.Color);

        //  Bottom left
        DrawSolidRect(renderCommand, new IntRect(rect.Left, rect.Bottom - thickness, rect.Left + length, rect.Bottom), caps.Color);
        DrawSolidRect(renderCommand, new IntRect(rect.Left, rect.Bottom - length, rect.Left + thickness, rect.Bottom - thickness), caps.Color);

        //  Bottom right
        DrawSolidRect(renderCommand, new IntRect(rect.Right - length, rect.Bottom - thickness, rect.Right, rect.Bottom), caps.Color);
        DrawSolidRect(renderCommand, new IntRect(rect.Right - thickness, rect.Bottom - length, rect.Right, rect.Bottom - thickness), caps.Color);
    }

    private void DrawSolidRect(RenderCommand<PixelTexture> renderCommand, IntRect rect, Vector4 color)
    {
        for (int y = rect.Top; y < rect.Bottom; y++)
        {
            for (int x = rect.Left; x < rect.Right; x++)
            {
                if (!renderCommand.ClipRect.Contains(x, y))
                {
                    continue;
                }

                if (IsOutsideCornerRadius(renderCommand, x, y))
                {
                    continue;
                }

                if (x < 0 || y < 0 || x >= _width || y >= _height)
                {
                    continue;
                }

                Vector4 currentColor = _renderTexture.GetPixel(x, y);
                _renderTexture.SetPixel(x, y, AlphaBlend(currentColor, color));
            }
        }
    }

    private static bool IsOutsideCornerRadius(RenderCommand<PixelTexture> renderCommand, int x, int y)
    {
        CornerRadius radius = renderCommand.CornerRadius;
        IntRect rect = renderCommand.Rect;
        int rightEdge = rect.Right - 1;
        int bottomEdge = rect.Bottom - 1;

        if (x < rect.Left + radius.TopLeft && y < rect.Top + radius.TopLeft)
        {
            int dx = x - (rect.Left + radius.TopLeft);
            int dy = y - (rect.Top + radius.TopLeft);
            return dx * dx + dy * dy > radius.TopLeft * radius.TopLeft;
        }

        if (x > rightEdge - radius.TopRight && y < rect.Top + radius.TopRight)
        {
            int dx = x - (rightEdge - radius.TopRight);
            int dy = y - (rect.Top + radius.TopRight);
            return dx * dx + dy * dy > radius.TopRight * radius.TopRight;
        }

        if (x > rightEdge - radius.BottomRight && y > bottomEdge - radius.BottomRight)
        {
            int dx = x - (rightEdge - radius.BottomRight);
            int dy = y - (bottomEdge - radius.BottomRight);
            return dx * dx + dy * dy > radius.BottomRight * radius.BottomRight;
        }

        if (x < rect.Left + radius.BottomLeft && y > bottomEdge - radius.BottomLeft)
        {
            int dx = x - (rect.Left + radius.BottomLeft);
            int dy = y - (bottomEdge - radius.BottomLeft);
            return dx * dx + dy * dy > radius.BottomLeft * radius.BottomLeft;
        }

        return false;
    }

    private void DrawCharacter(IntRect bbox, IntRect clipRect, IntRect uv, Vector4 color, PixelFontSDF font)
    {
        for (int y = bbox.Top; y < bbox.Bottom; y++)
        for (int x = bbox.Left; x < bbox.Right; x++)
        {
            //  Skip pixels that are outside the clip rect
            if (!clipRect.Contains(x, y))
            {
                continue;
            }
            
            //  Skip pixels that are outside the render target
            if (x < 0 || y < 0 || x >= _width || y >= _height)
            {
                continue;
            }
            
            var u = (int)RangeToRange(x, bbox.Left, bbox.Right, uv.Left, uv.Right);
            var v = (int)RangeToRange(y, bbox.Top, bbox.Bottom, uv.Top, uv.Bottom);
            
            Vector4 currentColor = _renderTexture.GetPixel(x, y);
            Vector4 sample = SampleSdf(font.Texture, font.Fwidth, u, v, Vector4.Zero, color);
            
            _renderTexture.SetPixel(x, y, AlphaBlend(currentColor, sample));
        }
    }

    private static Vector4 SampleSdf(PixelTexture sdf, float fwidth, int u, int v, Vector4 outsideColor, Vector4 insideColor)
    {
        //  Clamp to the atlas bounds; glyph quads can touch or cross the edge
        u = Math.Clamp(u, 0, sdf.Width - 1);
        v = Math.Clamp(v, 0, sdf.Height - 1);

        Vector4 s = sdf.GetPixel(u, v);
        float d = Median(s.X, s.Y, s.Z) - 0.5f;
        float w = Math.Clamp(d / fwidth + 0.5f, 0f, 1f);
        return Mix(outsideColor, insideColor, w);
    }

    private static float Median(float a, float b, float c)
    {
        return Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
    }

    private static Vector4 AlphaBlend(Vector4 background, Vector4 foreground)
    {
        float af = foreground.W;
        float ab = background.W;
        float a = af + ab * (1 - af);

        if (a <= 0)
        {
            return Vector4.Zero;
        }

        float r = (foreground.X * af + background.X * ab * (1 - af)) / a;
        float g = (foreground.Y * af + background.Y * ab * (1 - af)) / a;
        float b = (foreground.Z * af + background.Z * ab * (1 - af)) / a;
        return new Vector4(r, g, b, a);
    }
    
    private static Vector4 MultiplyBlend(Vector4 background, Vector4 foreground)
    {
        float af = foreground.W;
        float ab = background.W;
        
        float rf = foreground.X;
        float rb = background.X;
        
        float gf = foreground.Y;
        float gb = background.Y;
        
        float bf = foreground.Z;
        float bb = background.Z;

        float a = af * ab;
        float r = rf * rb;
        float g = gf * gb;
        float b = bf * bb;

        return new Vector4(r, g, b, a);
    }
    
    private static Vector4 Mix(Vector4 color1, Vector4 color2, float t)
    {
        return new Vector4(
            color1.X + (color2.X - color1.X) * t,
            color1.Y + (color2.Y - color1.Y) * t,
            color1.Z + (color2.Z - color1.Z) * t,
            color1.W + (color2.W - color1.W) * t
        );
    }
    
    private static float RangeToRange(float input, float low, float high, float newLow, float newHigh)
    {
        return ((input - low) / (high - low)) * (newHigh - newLow) + newLow;
    }
}