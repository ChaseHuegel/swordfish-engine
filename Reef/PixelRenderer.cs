using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Reef.Text;
using Reef.UI;

namespace Reef;

public sealed class PixelRenderer
{
    private readonly IntRect _screenBounds;
    private readonly PixelTexture _renderTexture;
    private readonly ITextEngine _textEngine;
    private readonly IDictionary<string, PixelFontSDF> _fonts;
    private readonly PixelFontSDF? _defaultFont;
    
    public PixelRenderer(int width, int height, ITextEngine textEngine, IDictionary<string, PixelFontSDF> fonts)
    {
        _screenBounds = new IntRect(0, 0, width, height);
        _textEngine = textEngine;
        _fonts = fonts;
        _defaultFont = fonts.Count > 0 ? fonts.Values.First() : null;
        _renderTexture = new PixelTexture(width, height);
    }
    
    public ReadOnlyCollection<Vector4> GetPixels()
    {
        return _renderTexture.GetPixels();
    }
    
    public void Render(List<RenderCommand<PixelTexture>> renderCommands)
    {
        _renderTexture.Clear();

        foreach (ref readonly RenderCommand<PixelTexture> renderCommand in CollectionsMarshal.AsSpan(renderCommands))
        {
            Draw(in renderCommand);
        }
    }
    
    private void Draw(in RenderCommand<PixelTexture> renderCommand)
    {
        //  Render rects.
        //  IntRect right and bottom bounds are exclusive, matching IntRect.Contains.
        IntRect clipped = ClipToScreen(renderCommand.Rect, renderCommand.ClipRect);
        if (renderCommand.RendererData != null)
        {
            DrawTexturedRect(in renderCommand, clipped);
        }
        else
        {
            DrawColorRect(in renderCommand, clipped);
        }

        DrawBorder(in renderCommand);
        DrawCornerCaps(in renderCommand);

        if (renderCommand.Text != null)
        {
            DrawText(in renderCommand);
        }
    }

    private void DrawText(in RenderCommand<PixelTexture> renderCommand)
    {
        TextLayout textLayout = renderCommand.TextLayout ?? _textEngine.Layout(renderCommand.FontOptions, renderCommand.Text!, renderCommand.Rect.Size.X);

        string? fontID = renderCommand.FontOptions.ID;
        if (fontID == null || !_fonts.TryGetValue(fontID, out PixelFontSDF font))
        {
            if (_defaultFont == null)
            {
                return;
            }

            font = _defaultFont.Value;
        }

        Vector4 color = renderCommand.Color;
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

            DrawCharacter(bbox, renderCommand.ClipRect, glyph.UV, glyph.ResolveColor(color), in font);
        }
    }

    private void DrawColorRect(in RenderCommand<PixelTexture> renderCommand, IntRect clipped)
    {
        //  A text element paints its background color behind the glyphs.
        Vector4 rectColor = renderCommand.Text == null ? renderCommand.Color : renderCommand.BackgroundColor;
        if (rectColor.W <= 0f)
        {
            return;
        }

        IntRect rect = renderCommand.Rect;
        CornerRadius radius = renderCommand.CornerRadius;
        int topRadius = Math.Max(radius.TopLeft, radius.TopRight);
        int bottomRadius = Math.Max(radius.BottomLeft, radius.BottomRight);
        bool rounded = topRadius > 0 || bottomRadius > 0;
        bool opaque = rectColor.W >= 1f;

        for (int y = clipped.Top; y < clipped.Bottom; y++)
        {
            Span<Vector4> row = _renderTexture.GetRowSpan(y);

            //  Corners only reach the top and bottom scanlines, so every other
            //  row is a contiguous run with no per-pixel corner test.
            if (!rounded || (y >= rect.Top + topRadius && y < rect.Bottom - bottomRadius))
            {
                if (opaque)
                {
                    row.Slice(clipped.Left, clipped.Size.X).Fill(rectColor);
                    continue;
                }

                for (int x = clipped.Left; x < clipped.Right; x++)
                {
                    row[x] = AlphaBlend(row[x], rectColor);
                }

                continue;
            }

            for (int x = clipped.Left; x < clipped.Right; x++)
            {
                if (IsOutsideCornerRadius(in renderCommand, x, y))
                {
                    continue;
                }

                row[x] = opaque ? rectColor : AlphaBlend(row[x], rectColor);
            }
        }
    }

    private void DrawTexturedRect(in RenderCommand<PixelTexture> renderCommand, IntRect clipped)
    {
        Vector4 color = renderCommand.Color;
        Vector4 backgroundColor = renderCommand.BackgroundColor;

        //  The background color is blended over the modulated texture, so a
        //  rect with a fully transparent color and background keeps the target.
        if (color.W <= 0f && backgroundColor.W <= 0f)
        {
            return;
        }

        PixelTexture texture = renderCommand.RendererData!;
        IntRect rect = renderCommand.Rect;
        CornerRadius radius = renderCommand.CornerRadius;
        int topRadius = Math.Max(radius.TopLeft, radius.TopRight);
        int bottomRadius = Math.Max(radius.BottomLeft, radius.BottomRight);
        bool rounded = topRadius > 0 || bottomRadius > 0;
        int textureWidth = texture.Width;
        int textureHeight = texture.Height;

        //  The clip test above guarantees a positive size, so both steps stay finite.
        float uStep = (float)textureWidth / rect.Size.X;
        float vStep = (float)textureHeight / rect.Size.Y;

        for (int y = clipped.Top; y < clipped.Bottom; y++)
        {
            Span<Vector4> row = _renderTexture.GetRowSpan(y);
            int v = Math.Clamp((int)((y - rect.Top) * vStep), 0, textureHeight - 1);
            Span<Vector4> textureRow = texture.GetRowSpan(v);
            bool straightRow = !rounded || (y >= rect.Top + topRadius && y < rect.Bottom - bottomRadius);

            for (int x = clipped.Left; x < clipped.Right; x++)
            {
                if (!straightRow && IsOutsideCornerRadius(in renderCommand, x, y))
                {
                    continue;
                }

                int u = Math.Clamp((int)((x - rect.Left) * uStep), 0, textureWidth - 1);
                Vector4 sample = MultiplyBlend(textureRow[u], color);
                //  Blend in any texture background color
                sample = AlphaBlend(backgroundColor, sample);
                if (sample.W <= 0f)
                {
                    continue;
                }

                row[x] = sample.W >= 1f ? sample : AlphaBlend(row[x], sample);
            }
        }
    }
    
    private void DrawBorder(in RenderCommand<PixelTexture> renderCommand)
    {
        BorderStyle border = renderCommand.Border;
        IntRect rect = renderCommand.Rect;

        int topThickness = ResolveThickness(border.TopThickness, border.Thickness);
        int rightThickness = ResolveThickness(border.RightThickness, border.Thickness);
        int bottomThickness = ResolveThickness(border.BottomThickness, border.Thickness);
        int leftThickness = ResolveThickness(border.LeftThickness, border.Thickness);
        Vector4 topColor = ResolveColor(border.TopColor, border.Color);
        Vector4 rightColor = ResolveColor(border.RightColor, border.Color);
        Vector4 bottomColor = ResolveColor(border.BottomColor, border.Color);
        Vector4 leftColor = ResolveColor(border.LeftColor, border.Color);

        bool drawTop = topThickness > 0 && topColor.W > 0f;
        bool drawRight = rightThickness > 0 && rightColor.W > 0f;
        bool drawBottom = bottomThickness > 0 && bottomColor.W > 0f;
        bool drawLeft = leftThickness > 0 && leftColor.W > 0f;

        //  Opposing sides share an axis, so they halve the available space. A lone side may use all of it.
        if (drawTop)
        {
            int limit = drawBottom ? rect.Size.Y / 2 : rect.Size.Y;
            DrawHorizontalSide(in renderCommand, border, rect.Left, rect.Right, rect.Top, Math.Min(topThickness, limit), topColor);
        }

        if (drawBottom)
        {
            int limit = drawTop ? rect.Size.Y / 2 : rect.Size.Y;
            int thickness = Math.Min(bottomThickness, limit);
            DrawHorizontalSide(in renderCommand, border, rect.Left, rect.Right, rect.Bottom - thickness, thickness, bottomColor);
        }

        if (drawLeft)
        {
            int limit = drawRight ? rect.Size.X / 2 : rect.Size.X;
            DrawVerticalSide(in renderCommand, border, rect.Top, rect.Bottom, rect.Left, Math.Min(leftThickness, limit), leftColor);
        }

        if (drawRight)
        {
            int limit = drawLeft ? rect.Size.X / 2 : rect.Size.X;
            int thickness = Math.Min(rightThickness, limit);
            DrawVerticalSide(in renderCommand, border, rect.Top, rect.Bottom, rect.Right - thickness, thickness, rightColor);
        }
    }

    private void DrawHorizontalSide(
        in RenderCommand<PixelTexture> command,
        in BorderStyle border,
        int left,
        int right,
        int top,
        int thickness,
        in Vector4 color)
    {
        if (thickness <= 0)
        {
            return;
        }

        BorderDash.Resolve(border, thickness, out int dash, out int gap);
        if (dash <= 0)
        {
            DrawSolidRect(in command, new IntRect(left, top, right, top + thickness), color);
            return;
        }

        var segments = new DashSegments(right - left, dash, gap);
        while (segments.MoveNext())
        {
            int start = left + segments.Offset;
            DrawSolidRect(in command, new IntRect(start, top, start + segments.SegmentLength, top + thickness), color);
        }
    }

    private void DrawVerticalSide(
        in RenderCommand<PixelTexture> command,
        in BorderStyle border,
        int top,
        int bottom,
        int left,
        int thickness,
        in Vector4 color)
    {
        if (thickness <= 0)
        {
            return;
        }

        BorderDash.Resolve(border, thickness, out int dash, out int gap);
        if (dash <= 0)
        {
            DrawSolidRect(in command, new IntRect(left, top, left + thickness, bottom), color);
            return;
        }

        var segments = new DashSegments(bottom - top, dash, gap);
        while (segments.MoveNext())
        {
            int start = top + segments.Offset;
            DrawSolidRect(in command, new IntRect(left, start, left + thickness, start + segments.SegmentLength), color);
        }
    }

    private static int ResolveThickness(int sideThickness, int fallback)
    {
        return sideThickness > 0 ? sideThickness : fallback;
    }

    private static Vector4 ResolveColor(Vector4 sideColor, Vector4 fallback)
    {
        return sideColor.W > 0f ? sideColor : fallback;
    }

    private void DrawCornerCaps(in RenderCommand<PixelTexture> renderCommand)
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
        DrawSolidRect(in renderCommand, new IntRect(rect.Left, rect.Top, rect.Left + length, rect.Top + thickness), caps.Color);
        DrawSolidRect(in renderCommand, new IntRect(rect.Left, rect.Top + thickness, rect.Left + thickness, rect.Top + length), caps.Color);

        //  Top right
        DrawSolidRect(in renderCommand, new IntRect(rect.Right - length, rect.Top, rect.Right, rect.Top + thickness), caps.Color);
        DrawSolidRect(in renderCommand, new IntRect(rect.Right - thickness, rect.Top + thickness, rect.Right, rect.Top + length), caps.Color);

        //  Bottom left
        DrawSolidRect(in renderCommand, new IntRect(rect.Left, rect.Bottom - thickness, rect.Left + length, rect.Bottom), caps.Color);
        DrawSolidRect(in renderCommand, new IntRect(rect.Left, rect.Bottom - length, rect.Left + thickness, rect.Bottom - thickness), caps.Color);

        //  Bottom right
        DrawSolidRect(in renderCommand, new IntRect(rect.Right - length, rect.Bottom - thickness, rect.Right, rect.Bottom), caps.Color);
        DrawSolidRect(in renderCommand, new IntRect(rect.Right - thickness, rect.Bottom - length, rect.Right, rect.Bottom - thickness), caps.Color);
    }

    private IntRect ClipToScreen(IntRect rect, IntRect clipRect)
    {
        return rect.Intersect(clipRect).Intersect(_screenBounds);
    }

    private void DrawSolidRect(in RenderCommand<PixelTexture> renderCommand, IntRect rect, Vector4 color)
    {
        IntRect clipped = ClipToScreen(rect, renderCommand.ClipRect);
        IntRect commandRect = renderCommand.Rect;
        CornerRadius radius = renderCommand.CornerRadius;
        int topRadius = Math.Max(radius.TopLeft, radius.TopRight);
        int bottomRadius = Math.Max(radius.BottomLeft, radius.BottomRight);
        bool rounded = topRadius > 0 || bottomRadius > 0;
        bool opaque = color.W >= 1f;

        for (int y = clipped.Top; y < clipped.Bottom; y++)
        {
            Span<Vector4> row = _renderTexture.GetRowSpan(y);

            //  Corners only reach the top and bottom scanlines, so every other
            //  row is a contiguous run with no per-pixel corner test.
            if (!rounded || (y >= commandRect.Top + topRadius && y < commandRect.Bottom - bottomRadius))
            {
                if (opaque)
                {
                    row.Slice(clipped.Left, clipped.Size.X).Fill(color);
                    continue;
                }

                for (int x = clipped.Left; x < clipped.Right; x++)
                {
                    row[x] = AlphaBlend(row[x], color);
                }

                continue;
            }

            for (int x = clipped.Left; x < clipped.Right; x++)
            {
                if (IsOutsideCornerRadius(in renderCommand, x, y))
                {
                    continue;
                }

                row[x] = opaque ? color : AlphaBlend(row[x], color);
            }
        }
    }

    private static bool IsOutsideCornerRadius(in RenderCommand<PixelTexture> renderCommand, int x, int y)
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

    private void DrawCharacter(IntRect bbox, IntRect clipRect, IntRect uv, Vector4 color, in PixelFontSDF font)
    {
        IntRect clipped = ClipToScreen(bbox, clipRect);
        int atlasWidth = font.Texture.Width;
        int atlasHeight = font.Texture.Height;
        float[] distances = font.Distances;
        float invFwidth = font.InvFwidth;

        //  The clip test guarantees a positive bbox size, so both steps stay finite.
        float uStep = (float)(uv.Right - uv.Left) / bbox.Size.X;
        float vStep = (float)(uv.Bottom - uv.Top) / bbox.Size.Y;

        for (int y = clipped.Top; y < clipped.Bottom; y++)
        {
            Span<Vector4> row = _renderTexture.GetRowSpan(y);
            int v = Math.Clamp((int)(uv.Top + (y - bbox.Top) * vStep), 0, atlasHeight - 1);
            int atlasRowOffset = v * atlasWidth;

            for (int x = clipped.Left; x < clipped.Right; x++)
            {
                int u = Math.Clamp((int)(uv.Left + (x - bbox.Left) * uStep), 0, atlasWidth - 1);
                float weight = Math.Clamp(distances[atlasRowOffset + u] * invFwidth + 0.5f, 0f, 1f);

                //  Most of a glyph quad lies outside the stroke, so leave the target alone.
                if (weight <= 0f)
                {
                    continue;
                }

                Vector4 sample = color * weight;
                row[x] = sample.W >= 1f ? sample : AlphaBlend(row[x], sample);
            }
        }
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
}