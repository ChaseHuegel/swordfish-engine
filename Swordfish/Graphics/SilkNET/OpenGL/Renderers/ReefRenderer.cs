using System.Numerics;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.Text;
using Reef.UI;
using Silk.NET.OpenGL;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Settings;
using Swordfish.UI.Reef;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class ReefRenderer : IScreenSpaceRenderStage
{
    private const string SOLID_REGION = "solid";
    private const int SOLID_SIZE = 4;
    private const uint VERTICES_PER_QUAD = 4;

    private readonly GL _gl;
    private readonly GLRenderContext _glRenderContext;
    private readonly RenderSettings _renderSettings;
    private readonly ReefContext _reefContext;
    private readonly DebugSettings _debugSettings;

    //  Every quad of the frame shares one vertex buffer. Quads keep paint order, and a
    //  run is closed only when the draw state changes.
    private readonly ReefBatch _batch = new();
    private readonly List<GLMaterial> _runMaterials = [];
    private GLMaterial? _activeMaterial;

    //  Rects and glyphs sample one packed atlas, so a frame without images is one draw.
    private readonly ReefAtlas? _atlas;
    private readonly Material? _atlasMaterial;
    private readonly float _solidU;
    private readonly float _solidV;

    //  If this is null, the renderer is attempting to render without having initialized.
    private readonly VertexArrayObject<float>? _vao;

    //  Texture coordinates applied to the quad that is being appended.
    private float _u0;
    private float _v0;
    private float _u1;
    private float _v1;

    public ReefRenderer(
        in GL gl,
        in GLContext glContext,
        in GLRenderContext glRenderContext,
        in RenderSettings renderSettings,
        in ReefContext reefContext,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs,
        in DebugSettings debugSettings,
        in ILogger logger
    ) {
        _gl = gl;
        _glRenderContext = glRenderContext;
        _renderSettings = renderSettings;
        _reefContext = reefContext;
        _debugSettings = debugSettings;

        if (!vfs.TryGetFile(AssetPaths.Shaders.At("ui_reef_atlas.glsl"), out PathInfo shaderFile))
        {
            logger.LogError("The shader source for OpenGL Reef UI was not found. Reef UI will not be rendered with OpenGL.");
            return;
        }

        if (!fileParseService.TryParse(shaderFile, out Shader shader))
        {
            logger.LogError("Failed to parse the OpenGL Reef UI shader. Reef UI will not be rendered with OpenGL.");
            return;
        }

        var sources = new List<ReefAtlas.Source>();
        foreach (ITypeface typeface in _reefContext.TextEngine.GetTypefaces())
        {
            AtlasInfo atlasInfo = typeface.GetAtlasInfo();
            if (!fileParseService.TryParse(atlasInfo.Path, out Texture atlas))
            {
                logger.LogError("Failed to load the texture for typeface (\"{ID}\") atlas \"{Path}\". This typeface will not render correctly.", typeface.ID, atlasInfo.Path);
                continue;
            }

            if (sources.Exists(source => source.Key == typeface.ID))
            {
                logger.LogWarning("Attempted to add a duplicate of typeface \"{ID}\".", typeface.ID);
                continue;
            }

            sources.Add(new ReefAtlas.Source(typeface.ID, atlas.Width, atlas.Height, atlas.Pixels));
        }

        sources.Add(new ReefAtlas.Source(SOLID_REGION, SOLID_SIZE, SOLID_SIZE, CreateSolidPixels(SOLID_SIZE)));
        _atlas = ReefAtlas.Pack(sources);

        ReefAtlas.Region solid = _atlas.GetRegion(SOLID_REGION);
        _solidU = (solid.OffsetX + (SOLID_SIZE / 2f)) / _atlas.Width;
        _solidV = (solid.OffsetY + (SOLID_SIZE / 2f)) / _atlas.Height;

        var atlasTexture = new Texture("ui_atlas", _atlas.Pixels, _atlas.Width, _atlas.Height, mipmaps: false);
        _atlasMaterial = new Material(shader, atlasTexture);

        //  Locations match the declarations in ui_reef_atlas.glsl. Every attribute is
        //  per instance, so the four corners of a quad read the same record.
        _vao = glContext.CreateVertexArrayObject(Array.Empty<float>());
        _vao.Bind();
        _vao.VertexBufferObject.Bind();
        SetInstanceAttribute(_vao, 8, ReefBatch.RECT_OFFSET);
        SetInstanceAttribute(_vao, 9, ReefBatch.UV_OFFSET);
        SetInstanceAttribute(_vao, 1, ReefBatch.COLOR_OFFSET);
        SetInstanceAttribute(_vao, 3, ReefBatch.CLIP_OFFSET);
        _vao.VertexBufferObject.Unbind();
        _vao.Unbind();
    }

    public void PreRender(double delta, RenderScene renderScene, bool isDepthPass)
    {
        if (isDepthPass || _vao == null || _atlas == null || _atlasMaterial == null)
        {
            return;
        }

        _reefContext.Builder.Debug = _debugSettings.UI.Get();
        List<RenderCommand<Material>> commands = _reefContext.Builder.Build(delta);

        _batch.Begin(_reefContext.Builder.Width, _reefContext.Builder.Height);
        _runMaterials.Clear();
        _activeMaterial = null;

        for (var i = 0; i < commands.Count; i++)
        {
            RenderCommand<Material> command = commands[i];
            IntRect clipRect = command.ClipRect;

            //  Only use the foreground color if this isn't text.
            Vector4 rectColor = command.Text == null ? command.Color : command.BackgroundColor;

            if (command.RendererData == null)
            {
                UseMaterial(_glRenderContext.BindMaterial(_atlasMaterial));
                SetSolidTextureCoordinates();
            }
            else
            {
                UseMaterial(_glRenderContext.BindMaterial(command.RendererData));
                SetFullTextureCoordinates();
            }

            AddRect(command.Rect, rectColor, clipRect);
            AddBorder(command, clipRect);
            AddCornerCaps(command, clipRect);

            if (command.Text == null)
            {
                continue;
            }

            string typefaceID = command.FontOptions.ID ?? _reefContext.TextEngine.GetDefaultTypeface().ID;
            if (!_atlas.TryGetRegion(typefaceID, out ReefAtlas.Region region))
            {
                //  No matching typeface, and no default is loaded
                continue;
            }

            TextLayout textLayout = command.TextLayout ?? _reefContext.TextEngine.Layout(command.FontOptions, command.Text, command.Rect.Size.X);
            if (textLayout.Glyphs.Length == 0)
            {
                continue;
            }

            UseMaterial(_glRenderContext.BindMaterial(_atlasMaterial));
            AddGlyphs(_atlas, textLayout, region, command, clipRect);
        }

        _batch.End();
    }

    public int Render(double delta, RenderScene renderScene, Action<ShaderProgram> shaderActivationCallback, bool isDepthPass)
    {
        if (_vao == null || isDepthPass || _batch.RunCount == 0)
        {
            return 0;
        }

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Set(EnableCap.DepthTest, false);
        _gl.PolygonMode(TriangleFace.FrontAndBack, _renderSettings.Wireframe ? PolygonMode.Line : PolygonMode.Fill);

        _vao.Bind();
        _vao.VertexBufferObject.UpdateStreamingData(_batch.Instances);

        for (var i = 0; i < _batch.RunCount; i++)
        {
            ReefBatch.Run run = _batch.GetRun(i);
            GLMaterial material = _runMaterials[run.State - 1];

            using GLMaterial.Scope _ = material.Use();
            shaderActivationCallback(material.ShaderProgram);

            _gl.DrawArraysInstancedBaseInstance(
                PrimitiveType.TriangleFan,
                first: 0,
                count: VERTICES_PER_QUAD,
                instancecount: (uint)run.InstanceCount,
                baseinstance: (uint)run.BaseInstance
            );
        }

        _gl.Disable(EnableCap.Blend);
        return _batch.RunCount;
    }

    private static void SetInstanceAttribute(VertexArrayObject<float> vao, uint location, int offset)
    {
        vao.SetVertexAttribute(location, 4, VertexAttribPointerType.Float, ReefBatch.INSTANCE_STRIDE, offset);
        vao.SetVertexAttributeDivisor(location, 1);
    }

    private static byte[] CreateSolidPixels(int size)
    {
        var pixels = new byte[size * size * 4];
        Array.Fill(pixels, (byte)255);
        return pixels;
    }

    private void UseMaterial(GLMaterial material)
    {
        if (ReferenceEquals(material, _activeMaterial))
        {
            return;
        }

        _activeMaterial = material;
        _runMaterials.Add(material);
        _batch.UseState(_runMaterials.Count);
    }

    private void SetSolidTextureCoordinates()
    {
        _u0 = _solidU;
        _v0 = _solidV;
        _u1 = _solidU;
        _v1 = _solidV;
    }

    private void SetFullTextureCoordinates()
    {
        _u0 = 0f;
        _v0 = 0f;
        _u1 = 1f;
        _v1 = 1f;
    }

    private void AddGlyphs(ReefAtlas atlas, TextLayout textLayout, in ReefAtlas.Region region, RenderCommand<Material> command, IntRect clipRect)
    {
        for (var i = 0; i < textLayout.Glyphs.Length; i++)
        {
            GlyphLayout glyph = textLayout.Glyphs[i];
            if (glyph.IsWhitespace)
            {
                continue;
            }

            var bbox = new IntRect(
                command.Rect.Left + glyph.BBOX.Left,
                command.Rect.Top + glyph.BBOX.Top,
                command.Rect.Left + glyph.BBOX.Right,
                command.Rect.Top + glyph.BBOX.Bottom
            );

            if (!clipRect.Intersects(bbox))
            {
                continue;
            }

            float u0 = region.MapU(glyph.UV.Left, atlas.Width);
            float u1 = region.MapU(glyph.UV.Right, atlas.Width);
            float v0 = region.MapV(glyph.UV.Bottom, atlas.Height);
            float v1 = region.MapV(glyph.UV.Top, atlas.Height);

            _batch.AddQuad(bbox, u0, v0, u1, v1, glyph.ResolveColor(command.Color), clipRect);
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

    private void AddBorder(RenderCommand<Material> command, IntRect clipRect)
    {
        BorderStyle border = command.Border;
        IntRect rect = command.Rect;

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
            AddHorizontalSide(border, clipRect, rect.Left, rect.Right, rect.Top, Math.Min(topThickness, limit), topColor);
        }

        if (drawBottom)
        {
            int limit = drawTop ? rect.Size.Y / 2 : rect.Size.Y;
            int thickness = Math.Min(bottomThickness, limit);
            AddHorizontalSide(border, clipRect, rect.Left, rect.Right, rect.Bottom - thickness, thickness, bottomColor);
        }

        if (drawLeft)
        {
            int limit = drawRight ? rect.Size.X / 2 : rect.Size.X;
            AddVerticalSide(border, clipRect, rect.Top, rect.Bottom, rect.Left, Math.Min(leftThickness, limit), leftColor);
        }

        if (drawRight)
        {
            int limit = drawLeft ? rect.Size.X / 2 : rect.Size.X;
            int thickness = Math.Min(rightThickness, limit);
            AddVerticalSide(border, clipRect, rect.Top, rect.Bottom, rect.Right - thickness, thickness, rightColor);
        }
    }

    private void AddHorizontalSide(
        in BorderStyle border,
        IntRect clipRect,
        int left,
        int right,
        int top,
        int thickness,
        in Vector4 color
    ) {
        if (thickness <= 0)
        {
            return;
        }

        BorderDash.Resolve(border, thickness, out int dash, out int gap);
        if (dash <= 0)
        {
            AddRect(new IntRect(left, top, right, top + thickness), color, clipRect);
            return;
        }

        var segments = new DashSegments(right - left, dash, gap);
        while (segments.MoveNext())
        {
            int start = left + segments.Offset;
            AddRect(new IntRect(start, top, start + segments.SegmentLength, top + thickness), color, clipRect);
        }
    }

    private void AddVerticalSide(
        in BorderStyle border,
        IntRect clipRect,
        int top,
        int bottom,
        int left,
        int thickness,
        in Vector4 color
    ) {
        if (thickness <= 0)
        {
            return;
        }

        BorderDash.Resolve(border, thickness, out int dash, out int gap);
        if (dash <= 0)
        {
            AddRect(new IntRect(left, top, left + thickness, bottom), color, clipRect);
            return;
        }

        var segments = new DashSegments(bottom - top, dash, gap);
        while (segments.MoveNext())
        {
            int start = top + segments.Offset;
            AddRect(new IntRect(left, start, left + thickness, start + segments.SegmentLength), color, clipRect);
        }
    }

    private void AddCornerCaps(RenderCommand<Material> command, IntRect clipRect)
    {
        CornerCapStyle caps = command.CornerCaps;
        IntRect rect = command.Rect;
        int maxLength = Math.Min(rect.Size.X, rect.Size.Y) / 2;
        int length = Math.Min(caps.Length, maxLength);
        int thickness = Math.Min(caps.Thickness, length);

        if (length <= 0 || thickness <= 0 || caps.Color.W <= 0f)
        {
            return;
        }

        //  Top left
        AddRect(new IntRect(rect.Left, rect.Top, rect.Left + length, rect.Top + thickness), caps.Color, clipRect);
        AddRect(new IntRect(rect.Left, rect.Top + thickness, rect.Left + thickness, rect.Top + length), caps.Color, clipRect);

        //  Top right
        AddRect(new IntRect(rect.Right - length, rect.Top, rect.Right, rect.Top + thickness), caps.Color, clipRect);
        AddRect(new IntRect(rect.Right - thickness, rect.Top + thickness, rect.Right, rect.Top + length), caps.Color, clipRect);

        //  Bottom left
        AddRect(new IntRect(rect.Left, rect.Bottom - thickness, rect.Left + length, rect.Bottom), caps.Color, clipRect);
        AddRect(new IntRect(rect.Left, rect.Bottom - length, rect.Left + thickness, rect.Bottom - thickness), caps.Color, clipRect);

        //  Bottom right
        AddRect(new IntRect(rect.Right - length, rect.Bottom - thickness, rect.Right, rect.Bottom), caps.Color, clipRect);
        AddRect(new IntRect(rect.Right - thickness, rect.Bottom - length, rect.Right, rect.Bottom - thickness), caps.Color, clipRect);
    }

    private void AddRect(IntRect rect, Vector4 color, IntRect clipRect)
    {
        _batch.AddQuad(rect, _u0, _v0, _u1, _v1, color, clipRect);
    }
}
