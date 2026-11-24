using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Reef.Text;
using Reef.UI;

namespace Reef;

public sealed class PixelRenderer
{
    private const int TileSize = 64;

    private readonly IntRect _screenBounds;

    //  The frame is stored with premultiplied alpha so blending needs no division.
    private readonly PixelTexture _renderTexture;
    private readonly ITextEngine _textEngine;
    private readonly IDictionary<string, PixelFontSDF> _fonts;
    private readonly PixelFontSDF? _defaultFont;
    private readonly int _tilesX;
    private readonly int _tilesY;
    private readonly int _tileCount;

    private List<RenderCommand<PixelTexture>>? _frameCommands;
    private TextLayout?[] _textLayouts = [];
    private IntRect[] _commandBounds = [];
    private int[] _tileCounts = [];
    private int[] _tileOffsets = [];
    private int[] _binnedCommands = [];
    
    public PixelRenderer(int width, int height, ITextEngine textEngine, IDictionary<string, PixelFontSDF> fonts)
    {
        _screenBounds = new IntRect(0, 0, width, height);
        _textEngine = textEngine;
        _fonts = fonts;
        _defaultFont = fonts.Count > 0 ? fonts.Values.First() : null;
        _renderTexture = new PixelTexture(width, height);
        _tilesX = (width + TileSize - 1) / TileSize;
        _tilesY = (height + TileSize - 1) / TileSize;
        _tileCount = _tilesX * _tilesY;
    }
    
    /// <summary>
    ///     Returns a copy of the frame with straight (unpremultiplied) alpha.
    /// </summary>
    public ReadOnlyCollection<Vector4> GetPixels()
    {
        ReadOnlySpan<Vector4> premultiplied = _renderTexture.AsSpan();
        var pixels = new Vector4[premultiplied.Length];
        for (var i = 0; i < premultiplied.Length; i++)
        {
            Vector4 pixel = premultiplied[i];
            if (pixel.W <= 0f || pixel.W >= 1f)
            {
                pixels[i] = pixel;
                continue;
            }

            pixels[i] = new Vector4(pixel.X / pixel.W, pixel.Y / pixel.W, pixel.Z / pixel.W, pixel.W);
        }

        return Array.AsReadOnly(pixels);
    }
    
    public void Render(List<RenderCommand<PixelTexture>> renderCommands)
    {
        ReadOnlySpan<RenderCommand<PixelTexture>> commands = CollectionsMarshal.AsSpan(renderCommands);
        _frameCommands = renderCommands;

        //  An opaque command that covers the screen overwrites every earlier
        //  command, so the frame does not need to start transparent.
        if (!HasOpaqueScreenCover(commands))
        {
            _renderTexture.Clear();
        }

        ResolveTextLayouts(commands);
        BinCommands(commands);

        if (Environment.ProcessorCount > 1 && !OperatingSystem.IsBrowser())
        {
            Parallel.For(0, _tileCount, RenderTile);
            return;
        }

        //  A single-threaded runtime keeps the tiling, because a tile still fits
        //  in cache while its commands rasterize.
        for (var tileIndex = 0; tileIndex < _tileCount; tileIndex++)
        {
            RenderTile(tileIndex);
        }
    }

    private bool HasOpaqueScreenCover(ReadOnlySpan<RenderCommand<PixelTexture>> commands)
    {
        for (var i = 0; i < commands.Length; i++)
        {
            RenderCommand<PixelTexture> command = commands[i];
            if (command.RendererData != null)
            {
                continue;
            }

            //  A text element paints its background color behind the glyphs.
            Vector4 color = command.Text == null ? command.Color : command.BackgroundColor;
            if (color.W < 1f)
            {
                continue;
            }

            CornerRadius radius = command.CornerRadius;
            if (radius.TopLeft > 0 || radius.TopRight > 0 || radius.BottomLeft > 0 || radius.BottomRight > 0)
            {
                continue;
            }

            if (CoversScreen(command.Rect) && CoversScreen(command.ClipRect))
            {
                return true;
            }
        }

        return false;
    }

    private bool CoversScreen(IntRect rect)
    {
        return rect.Left <= _screenBounds.Left
            && rect.Top <= _screenBounds.Top
            && rect.Right >= _screenBounds.Right
            && rect.Bottom >= _screenBounds.Bottom;
    }

    private void ResolveTextLayouts(ReadOnlySpan<RenderCommand<PixelTexture>> commands)
    {
        EnsureCommandCapacity(commands.Length);

        for (var i = 0; i < commands.Length; i++)
        {
            RenderCommand<PixelTexture> command = commands[i];
            _textLayouts[i] = command.Text == null
                ? null
                : command.TextLayout ?? _textEngine.Layout(command.FontOptions, command.Text, command.Rect.Size.X);
        }
    }

    private void BinCommands(ReadOnlySpan<RenderCommand<PixelTexture>> commands)
    {
        if (_tileCounts.Length < _tileCount)
        {
            _tileCounts = new int[_tileCount];
        }

        if (_tileOffsets.Length < _tileCount + 1)
        {
            _tileOffsets = new int[_tileCount + 1];
        }

        Array.Clear(_tileCounts, 0, _tileCount);

        var requiredCommands = 0;
        for (var i = 0; i < commands.Length; i++)
        {
            IntRect bounds = ResolveCommandBounds(commands[i], _textLayouts[i]);
            _commandBounds[i] = bounds;
            if (bounds.Size.X <= 0 || bounds.Size.Y <= 0)
            {
                continue;
            }

            int minTileX = bounds.Left / TileSize;
            int maxTileX = (bounds.Right - 1) / TileSize;
            int minTileY = bounds.Top / TileSize;
            int maxTileY = (bounds.Bottom - 1) / TileSize;

            for (int tileY = minTileY; tileY <= maxTileY; tileY++)
            for (int tileX = minTileX; tileX <= maxTileX; tileX++)
            {
                _tileCounts[tileY * _tilesX + tileX]++;
            }

            requiredCommands += (maxTileX - minTileX + 1) * (maxTileY - minTileY + 1);
        }

        if (_binnedCommands.Length < requiredCommands)
        {
            _binnedCommands = new int[requiredCommands];
        }

        var offset = 0;
        for (var tileIndex = 0; tileIndex < _tileCount; tileIndex++)
        {
            _tileOffsets[tileIndex] = offset;
            offset += _tileCounts[tileIndex];
            _tileCounts[tileIndex] = _tileOffsets[tileIndex];
        }

        _tileOffsets[_tileCount] = offset;

        //  Commands are visited in paint order, so every tile keeps painter's order.
        for (var i = 0; i < commands.Length; i++)
        {
            IntRect bounds = _commandBounds[i];
            if (bounds.Size.X <= 0 || bounds.Size.Y <= 0)
            {
                continue;
            }

            int minTileX = bounds.Left / TileSize;
            int maxTileX = (bounds.Right - 1) / TileSize;
            int minTileY = bounds.Top / TileSize;
            int maxTileY = (bounds.Bottom - 1) / TileSize;

            for (int tileY = minTileY; tileY <= maxTileY; tileY++)
            for (int tileX = minTileX; tileX <= maxTileX; tileX++)
            {
                _binnedCommands[_tileCounts[tileY * _tilesX + tileX]++] = i;
            }
        }
    }

    private void EnsureCommandCapacity(int commandCount)
    {
        if (_textLayouts.Length >= commandCount)
        {
            return;
        }

        _textLayouts = new TextLayout?[commandCount];
        _commandBounds = new IntRect[commandCount];
    }

    private void RenderTile(int tileIndex)
    {
        ReadOnlySpan<RenderCommand<PixelTexture>> commands = CollectionsMarshal.AsSpan(_frameCommands!);
        IntRect tileRect = ResolveTileRect(tileIndex);

        int start = _tileOffsets[tileIndex];
        int end = _tileOffsets[tileIndex + 1];
        for (var i = start; i < end; i++)
        {
            int commandIndex = _binnedCommands[i];
            var context = new DrawContext(commands[commandIndex], commands[commandIndex].ClipRect.Intersect(tileRect), _textLayouts[commandIndex]);
            Draw(in context);
        }
    }

    private IntRect ResolveTileRect(int tileIndex)
    {
        int tileX = tileIndex % _tilesX;
        int tileY = tileIndex / _tilesX;
        int left = tileX * TileSize;
        int top = tileY * TileSize;
        return new IntRect(
            left,
            top,
            Math.Min(left + TileSize, _renderTexture.Width),
            Math.Min(top + TileSize, _renderTexture.Height));
    }

    private IntRect ResolveCommandBounds(in RenderCommand<PixelTexture> command, TextLayout? textLayout)
    {
        IntRect bounds = command.Rect;
        if (textLayout == null)
        {
            return ClipToScreen(bounds, command.ClipRect);
        }

        //  Glyph bounds are element-local and can overhang the rect when text does not wrap.
        GlyphLayout[] glyphs = textLayout.Value.Glyphs;
        for (var i = 0; i < glyphs.Length; i++)
        {
            GlyphLayout glyph = glyphs[i];
            if (glyph.IsWhitespace)
            {
                continue;
            }

            bounds = bounds.Union(new IntRect(
                command.Rect.Left + glyph.BBOX.Left,
                command.Rect.Top + glyph.BBOX.Top,
                command.Rect.Left + glyph.BBOX.Right,
                command.Rect.Top + glyph.BBOX.Bottom));
        }

        return ClipToScreen(bounds, command.ClipRect);
    }

    private void Draw(in DrawContext context)
    {
        //  Render rects.
        //  IntRect right and bottom bounds are exclusive, matching IntRect.Contains.
        IntRect clipped = ClipToScreen(context.Command.Rect, context.ClipRect);
        if (clipped.Size.X > 0 && clipped.Size.Y > 0)
        {
            if (context.Command.RendererData != null)
            {
                DrawTexturedRect(in context, clipped);
            }
            else
            {
                DrawColorRect(in context, clipped);
            }
        }

        DrawBorder(in context);
        DrawCornerCaps(in context);

        if (context.Command.Text != null)
        {
            DrawText(in context);
        }
    }

    private void DrawText(in DrawContext context)
    {
        if (context.TextLayout == null)
        {
            return;
        }

        TextLayout textLayout = context.TextLayout.Value;

        string? fontID = context.Command.FontOptions.ID;
        if (fontID == null || !_fonts.TryGetValue(fontID, out PixelFontSDF font))
        {
            if (_defaultFont == null)
            {
                return;
            }

            font = _defaultFont.Value;
        }

        Vector4 color = context.Command.Color;
        for (var i = 0; i < textLayout.Glyphs.Length; i++)
        {
            GlyphLayout glyph = textLayout.Glyphs[i];
            if (glyph.IsWhitespace)
            {
                continue;
            }

            var bbox = new IntRect(
                context.Command.Rect.Left + glyph.BBOX.Left,
                context.Command.Rect.Top + glyph.BBOX.Top,
                context.Command.Rect.Left + glyph.BBOX.Right,
                context.Command.Rect.Top + glyph.BBOX.Bottom
            );

            DrawCharacter(bbox, context.ClipRect, glyph.UV, glyph.ResolveColor(color), in font);
        }
    }

    private void DrawColorRect(in DrawContext context, IntRect clipped)
    {
        //  A text element paints its background color behind the glyphs.
        Vector4 rectColor = ToPremultiplied(context.Command.Text == null ? context.Command.Color : context.Command.BackgroundColor);
        if (rectColor.W <= 0f)
        {
            return;
        }

        IntRect rect = context.Command.Rect;
        CornerRadius radius = context.Command.CornerRadius;
        int topRadius = Math.Max(radius.TopLeft, radius.TopRight);
        int bottomRadius = Math.Max(radius.BottomLeft, radius.BottomRight);
        bool rounded = topRadius > 0 || bottomRadius > 0;
        bool spanCorners = rounded && !CornersOverlap(radius, rect);
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

                BlendRow(row.Slice(clipped.Left, clipped.Size.X), rectColor);
                continue;
            }

            //  A corner row still paints one contiguous run between the two radii.
            if (spanCorners)
            {
                ResolveRadiusSpan(radius, rect, y, out int rowLeft, out int rowRight);
                int start = Math.Max(clipped.Left, rowLeft);
                int end = Math.Min(clipped.Right, rowRight);
                if (end <= start)
                {
                    continue;
                }

                if (opaque)
                {
                    row.Slice(start, end - start).Fill(rectColor);
                    continue;
                }

                BlendRow(row.Slice(start, end - start), rectColor);
                continue;
            }

            for (int x = clipped.Left; x < clipped.Right; x++)
            {
                if (IsOutsideCornerRadius(in context, x, y))
                {
                    continue;
                }

                row[x] = opaque ? rectColor : AlphaBlend(row[x], rectColor);
            }
        }
    }

    private void DrawTexturedRect(in DrawContext context, IntRect clipped)
    {
        Vector4 color = context.Command.Color;
        Vector4 backgroundColor = context.Command.BackgroundColor;

        //  The background color is blended over the modulated texture, so a
        //  rect with a fully transparent color and background keeps the target.
        if (color.W <= 0f && backgroundColor.W <= 0f)
        {
            return;
        }

        PixelTexture texture = context.Command.RendererData!;
        IntRect rect = context.Command.Rect;
        CornerRadius radius = context.Command.CornerRadius;
        int topRadius = Math.Max(radius.TopLeft, radius.TopRight);
        int bottomRadius = Math.Max(radius.BottomLeft, radius.BottomRight);
        bool rounded = topRadius > 0 || bottomRadius > 0;
        bool spanCorners = rounded && !CornersOverlap(radius, rect);
        int textureWidth = texture.Width;
        int textureHeight = texture.Height;
        Vector4 backgroundColorPremultiplied = ToPremultiplied(backgroundColor);

        //  The clip test above guarantees a positive size, so both steps stay finite.
        float uStep = (float)textureWidth / rect.Size.X;
        float vStep = (float)textureHeight / rect.Size.Y;

        for (int y = clipped.Top; y < clipped.Bottom; y++)
        {
            Span<Vector4> row = _renderTexture.GetRowSpan(y);
            int v = Math.Clamp((int)((y - rect.Top) * vStep), 0, textureHeight - 1);
            Span<Vector4> textureRow = texture.GetRowSpan(v);
            bool straightRow = !rounded || (y >= rect.Top + topRadius && y < rect.Bottom - bottomRadius);
            int start = clipped.Left;
            int end = clipped.Right;

            //  A corner row still paints one contiguous run between the two radii.
            if (!straightRow && spanCorners)
            {
                ResolveRadiusSpan(radius, rect, y, out int rowLeft, out int rowRight);
                start = Math.Max(start, rowLeft);
                end = Math.Min(end, rowRight);
            }

            for (int x = start; x < end; x++)
            {
                if (!straightRow && !spanCorners && IsOutsideCornerRadius(in context, x, y))
                {
                    continue;
                }

                int u = Math.Clamp((int)((x - rect.Left) * uStep), 0, textureWidth - 1);
                Vector4 sample = ToPremultiplied(MultiplyBlend(textureRow[u], color));
                //  Blend in any texture background color
                sample = AlphaBlend(backgroundColorPremultiplied, sample);
                if (sample.W <= 0f)
                {
                    continue;
                }

                row[x] = sample.W >= 1f ? sample : AlphaBlend(row[x], sample);
            }
        }
    }
    
    private void DrawBorder(in DrawContext context)
    {
        BorderStyle border = context.Command.Border;
        IntRect rect = context.Command.Rect;

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
            DrawHorizontalSide(in context, border, rect.Left, rect.Right, rect.Top, Math.Min(topThickness, limit), topColor);
        }

        if (drawBottom)
        {
            int limit = drawTop ? rect.Size.Y / 2 : rect.Size.Y;
            int thickness = Math.Min(bottomThickness, limit);
            DrawHorizontalSide(in context, border, rect.Left, rect.Right, rect.Bottom - thickness, thickness, bottomColor);
        }

        if (drawLeft)
        {
            int limit = drawRight ? rect.Size.X / 2 : rect.Size.X;
            DrawVerticalSide(in context, border, rect.Top, rect.Bottom, rect.Left, Math.Min(leftThickness, limit), leftColor);
        }

        if (drawRight)
        {
            int limit = drawLeft ? rect.Size.X / 2 : rect.Size.X;
            int thickness = Math.Min(rightThickness, limit);
            DrawVerticalSide(in context, border, rect.Top, rect.Bottom, rect.Right - thickness, thickness, rightColor);
        }
    }

    private void DrawHorizontalSide(
        in DrawContext context,
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
            DrawSolidRect(in context, new IntRect(left, top, right, top + thickness), color);
            return;
        }

        var segments = new DashSegments(right - left, dash, gap);
        while (segments.MoveNext())
        {
            int start = left + segments.Offset;
            DrawSolidRect(in context, new IntRect(start, top, start + segments.SegmentLength, top + thickness), color);
        }
    }

    private void DrawVerticalSide(
        in DrawContext context,
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
            DrawSolidRect(in context, new IntRect(left, top, left + thickness, bottom), color);
            return;
        }

        var segments = new DashSegments(bottom - top, dash, gap);
        while (segments.MoveNext())
        {
            int start = top + segments.Offset;
            DrawSolidRect(in context, new IntRect(left, start, left + thickness, start + segments.SegmentLength), color);
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

    private void DrawCornerCaps(in DrawContext context)
    {
        CornerCapStyle caps = context.Command.CornerCaps;
        IntRect rect = context.Command.Rect;
        int maxLength = Math.Min(rect.Size.X, rect.Size.Y) / 2;
        int length = Math.Min(caps.Length, maxLength);
        int thickness = Math.Min(caps.Thickness, length);

        if (length <= 0 || thickness <= 0 || caps.Color.W <= 0f)
        {
            return;
        }

        //  Top left
        DrawSolidRect(in context, new IntRect(rect.Left, rect.Top, rect.Left + length, rect.Top + thickness), caps.Color);
        DrawSolidRect(in context, new IntRect(rect.Left, rect.Top + thickness, rect.Left + thickness, rect.Top + length), caps.Color);

        //  Top right
        DrawSolidRect(in context, new IntRect(rect.Right - length, rect.Top, rect.Right, rect.Top + thickness), caps.Color);
        DrawSolidRect(in context, new IntRect(rect.Right - thickness, rect.Top + thickness, rect.Right, rect.Top + length), caps.Color);

        //  Bottom left
        DrawSolidRect(in context, new IntRect(rect.Left, rect.Bottom - thickness, rect.Left + length, rect.Bottom), caps.Color);
        DrawSolidRect(in context, new IntRect(rect.Left, rect.Bottom - length, rect.Left + thickness, rect.Bottom - thickness), caps.Color);

        //  Bottom right
        DrawSolidRect(in context, new IntRect(rect.Right - length, rect.Bottom - thickness, rect.Right, rect.Bottom), caps.Color);
        DrawSolidRect(in context, new IntRect(rect.Right - thickness, rect.Bottom - length, rect.Right, rect.Bottom - thickness), caps.Color);
    }

    private IntRect ClipToScreen(IntRect rect, IntRect clipRect)
    {
        return rect.Intersect(clipRect).Intersect(_screenBounds);
    }

    private void DrawSolidRect(in DrawContext context, IntRect rect, Vector4 color)
    {
        IntRect clipped = ClipToScreen(rect, context.ClipRect);
        if (clipped.Size.X <= 0 || clipped.Size.Y <= 0)
        {
            return;
        }

        IntRect commandRect = context.Command.Rect;
        CornerRadius radius = context.Command.CornerRadius;
        int topRadius = Math.Max(radius.TopLeft, radius.TopRight);
        int bottomRadius = Math.Max(radius.BottomLeft, radius.BottomRight);
        bool rounded = topRadius > 0 || bottomRadius > 0;
        bool spanCorners = rounded && !CornersOverlap(radius, commandRect);
        Vector4 premultiplied = ToPremultiplied(color);
        bool opaque = premultiplied.W >= 1f;

        for (int y = clipped.Top; y < clipped.Bottom; y++)
        {
            Span<Vector4> row = _renderTexture.GetRowSpan(y);

            //  Corners only reach the top and bottom scanlines, so every other
            //  row is a contiguous run with no per-pixel corner test.
            if (!rounded || (y >= commandRect.Top + topRadius && y < commandRect.Bottom - bottomRadius))
            {
                if (opaque)
                {
                    row.Slice(clipped.Left, clipped.Size.X).Fill(premultiplied);
                    continue;
                }

                BlendRow(row.Slice(clipped.Left, clipped.Size.X), premultiplied);
                continue;
            }

            //  A corner row still paints one contiguous run between the two radii.
            if (spanCorners)
            {
                ResolveRadiusSpan(radius, commandRect, y, out int rowLeft, out int rowRight);
                int start = Math.Max(clipped.Left, rowLeft);
                int end = Math.Min(clipped.Right, rowRight);
                if (end <= start)
                {
                    continue;
                }

                if (opaque)
                {
                    row.Slice(start, end - start).Fill(premultiplied);
                    continue;
                }

                BlendRow(row.Slice(start, end - start), premultiplied);
                continue;
            }

            for (int x = clipped.Left; x < clipped.Right; x++)
            {
                if (IsOutsideCornerRadius(in context, x, y))
                {
                    continue;
                }

                row[x] = opaque ? premultiplied : AlphaBlend(row[x], premultiplied);
            }
        }
    }

    private static bool CornersOverlap(in CornerRadius radius, in IntRect rect)
    {
        int width = rect.Size.X;
        int height = rect.Size.Y;
        int smallest = Math.Min(width, height);

        return radius.TopLeft + radius.TopRight > width
            || radius.BottomLeft + radius.BottomRight > width
            || radius.TopLeft + radius.BottomLeft > height
            || radius.TopRight + radius.BottomRight > height
            || radius.TopLeft + radius.BottomRight > smallest
            || radius.TopRight + radius.BottomLeft > smallest;
    }

    /// <summary>
    ///     Returns the horizontal run of a scanline that the corner radii leave visible.
    ///     Call it only when <see cref="CornersOverlap" /> is false.
    /// </summary>
    private static void ResolveRadiusSpan(in CornerRadius radius, in IntRect rect, int y, out int left, out int right)
    {
        left = rect.Left;
        right = rect.Right;
        int rightEdge = rect.Right - 1;
        int bottomEdge = rect.Bottom - 1;

        if (y < rect.Top + radius.TopLeft)
        {
            int offset = RadiusOffset(radius.TopLeft, y - (rect.Top + radius.TopLeft));
            left = Math.Max(left, rect.Left + radius.TopLeft - offset);
        }

        if (y > bottomEdge - radius.BottomLeft)
        {
            int offset = RadiusOffset(radius.BottomLeft, y - (bottomEdge - radius.BottomLeft));
            left = Math.Max(left, rect.Left + radius.BottomLeft - offset);
        }

        if (y < rect.Top + radius.TopRight)
        {
            int offset = RadiusOffset(radius.TopRight, y - (rect.Top + radius.TopRight));
            right = Math.Min(right, rightEdge - radius.TopRight + offset + 1);
        }

        if (y > bottomEdge - radius.BottomRight)
        {
            int offset = RadiusOffset(radius.BottomRight, y - (bottomEdge - radius.BottomRight));
            right = Math.Min(right, rightEdge - radius.BottomRight + offset + 1);
        }
    }

    private static int RadiusOffset(int radius, int dy)
    {
        int squared = radius * radius - dy * dy;
        return squared > 0 ? (int)Math.Sqrt(squared) : 0;
    }

    private static bool IsOutsideCornerRadius(in DrawContext context, int x, int y)
    {
        CornerRadius radius = context.Command.CornerRadius;
        IntRect rect = context.Command.Rect;
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
        if (clipped.Size.X <= 0 || clipped.Size.Y <= 0)
        {
            return;
        }

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

                Vector4 sample = ToPremultiplied(color * weight);
                row[x] = sample.W >= 1f ? sample : AlphaBlend(row[x], sample);
            }
        }
    }

    /// <summary>
    ///     Composites one premultiplied color over a scanline. The vector path
    ///     maps to whatever SIMD width the runtime provides, and a scalar tail
    ///     covers runtimes without hardware acceleration.
    /// </summary>
    private static void BlendRow(Span<Vector4> row, Vector4 foreground)
    {
        float inverseAlpha = 1f - foreground.W;
        Span<float> lanes = MemoryMarshal.Cast<Vector4, float>(row);
        int laneWidth = Vector<float>.Count;
        var lane = 0;

        if (Vector.IsHardwareAccelerated && lanes.Length >= laneWidth)
        {
            Span<float> foregroundLanes = stackalloc float[laneWidth];
            Span<float> inverseLanes = stackalloc float[laneWidth];
            for (var channel = 0; channel < laneWidth; channel++)
            {
                foregroundLanes[channel] = ResolveChannel(foreground, channel % 4);
                inverseLanes[channel] = inverseAlpha;
            }

            var foregroundVector = new Vector<float>(foregroundLanes);
            var inverseVector = new Vector<float>(inverseLanes);
            for (; lane <= lanes.Length - laneWidth; lane += laneWidth)
            {
                var background = new Vector<float>(lanes.Slice(lane, laneWidth));
                (foregroundVector + background * inverseVector).CopyTo(lanes.Slice(lane, laneWidth));
            }
        }

        for (; lane < lanes.Length; lane += 4)
        {
            int pixel = lane / 4;
            Vector4 background = row[pixel];
            row[pixel] = new Vector4(
                foreground.X + background.X * inverseAlpha,
                foreground.Y + background.Y * inverseAlpha,
                foreground.Z + background.Z * inverseAlpha,
                foreground.W + background.W * inverseAlpha);
        }
    }

    private static float ResolveChannel(Vector4 color, int channel)
    {
        return channel switch
        {
            0 => color.X,
            1 => color.Y,
            2 => color.Z,
            _ => color.W,
        };
    }

    /// <summary>
    ///     Converts a straight color to premultiplied alpha.
    /// </summary>
    private static Vector4 ToPremultiplied(Vector4 color)
    {
        return new Vector4(color.X * color.W, color.Y * color.W, color.Z * color.W, color.W);
    }

    /// <summary>
    ///     Composites a premultiplied foreground over a premultiplied background.
    ///     Both operands are premultiplied, so no division is needed.
    /// </summary>
    private static Vector4 AlphaBlend(Vector4 background, Vector4 foreground)
    {
        float inverseAlpha = 1f - foreground.W;
        return new Vector4(
            foreground.X + background.X * inverseAlpha,
            foreground.Y + background.Y * inverseAlpha,
            foreground.Z + background.Z * inverseAlpha,
            foreground.W + background.W * inverseAlpha);
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

    /// <summary>
    ///     One render command resolved for a tile: the command itself, the clip
    ///     rect for the tile, and the text layout resolved on the calling thread.
    /// </summary>
    private readonly struct DrawContext(
        RenderCommand<PixelTexture> command,
        IntRect clipRect,
        TextLayout? textLayout)
    {
        public readonly RenderCommand<PixelTexture> Command = command;
        public readonly IntRect ClipRect = clipRect;
        public readonly TextLayout? TextLayout = textLayout;
    }
}
