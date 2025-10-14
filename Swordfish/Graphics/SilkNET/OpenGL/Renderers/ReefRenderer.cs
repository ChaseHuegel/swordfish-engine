using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.Text;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Util;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.Settings;
using Swordfish.Types;
using Swordfish.UI.Reef;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class ReefRenderer(
    in GL gl,
    in GLContext glContext,
    in RenderSettings renderSettings,
    in ReefContext reefContext,
    in IFileParseService fileParseService,
    in VirtualFileSystem vfs,
    in ILogger logger) : IUnlitRenderStage
{
    private readonly GL _gl = gl;
    private readonly GLContext _glContext = glContext;
    private readonly RenderSettings _renderSettings = renderSettings;
    private readonly ReefContext _reefContext = reefContext;
    private readonly IFileParseService _fileParseService = fileParseService;
    private readonly VirtualFileSystem _vfs = vfs;
    private readonly ILogger _logger = logger;
    private readonly Dictionary<string, Material> _typefaceMaterials = [];

    //  If either of these is null, the renderer is attempting to render without having initialized.
    private GLRenderContext _renderContext = null!;
    private ShaderProgram _defaultShader = null!;
    
    private VertexArrayObject<float>? _vao;
    
    //  TODO improve and bring back batching UI.
    //       Instance batches were being created by material and font, however this was problematic
    //       because images would render in a separate batch after simple rects and results in
    //       depth being incorrect, where a parent Image renders over a child rect or text.
    //       -
    //       Using a depth buffer is insufficient because a parent isn't always in the same
    //       draw call as the child in that scenario. Batches are going to have to be smarter,
    //       likely where an element can break the batch for its entire tree of elements.
    //       -
    //       For the time being, UI is not being batched for the sake of being able to focus
    //       on more important issues. For the time, performance here isn't a particular concern.
    private readonly Dictionary<RenderCommand<Material>, InstanceVertexData> _instances = new(/*new MaterialRenderCommandComparer()*/);

    public void Initialize(IRenderContext renderContext)
    {
        if (renderContext is not GLRenderContext glRenderContext)
        {
            throw new NotSupportedException($"{nameof(ReefRenderer)} only supports an OpenGL {nameof(IRenderContext)}.");
        }
        
        if (!_vfs.TryGetFile(AssetPaths.Shaders.At("ui_reef.glsl"), out PathInfo defaultUIShaderFile))
        {
            _logger.LogError("The shader source for OpenGL Reef UI was not found. Reef UI will not be rendered with OpenGL.");
            return;
        }

        if (!_fileParseService.TryParse(defaultUIShaderFile, out Shader shader))
        {
            _logger.LogError("Failed to parse the OpenGL Reef UI shader. Reef UI will not be rendered with OpenGL.");
            return;
        }
        
        if (!_vfs.TryGetFile(AssetPaths.Shaders.At("ui_reef_msdf.glsl"), out PathInfo textShaderFile))
        {
            _logger.LogError("The shader source for OpenGL Reef UI MSDF text was not found. Reef UI will not be rendered with OpenGL.");
            return;
        }
        
        if (!_fileParseService.TryParse(textShaderFile, out Shader textShader))
        {
            _logger.LogError("Failed to parse the OpenGL Reef UI MSDF text shader. Reef UI will not be rendered with OpenGL.");
            return;
        }

        foreach (ITypeface typeface in _reefContext.TextEngine.GetTypefaces())
        {
            AtlasInfo atlasInfo = typeface.GetAtlasInfo();
            if (!_fileParseService.TryParse(atlasInfo.Path, out Texture atlas))
            {
                _logger.LogError("Failed to load the texture for typeface (\"{ID}\") atlas \"{Path}\". This typeface will not render correctly.", typeface.ID, atlasInfo.Path);
                continue;
            }

            if (_typefaceMaterials.ContainsKey(typeface.ID))
            {
                _logger.LogWarning("Attempted to add a duplicate of typeface \"{ID}\".", typeface.ID);
                continue;
            }

            var material = new Material(textShader, atlas);
            _typefaceMaterials.Add(typeface.ID, material);
        }

        _renderContext = glRenderContext;
        
        _vao = _glContext.CreateVertexArrayObject(Array.Empty<float>());
        _vao.Bind();
        _vao.VertexBufferObject.Bind();
        _vao.SetVertexAttribute(0, 3, VertexAttribPointerType.Float, 14, 0);
        _vao.SetVertexAttribute(1, 4, VertexAttribPointerType.Float, 14, 3);
        _vao.SetVertexAttribute(2, 3, VertexAttribPointerType.Float, 14, 7);
        _vao.SetVertexAttribute(3, 4, VertexAttribPointerType.Float, 14, 10);
        _vao.VertexBufferObject.Unbind();
        _vao.Unbind();

        _defaultShader = shader.CreateProgram(_glContext);
        _defaultShader.BindAttributeLocation("in_position", 0);
        _defaultShader.BindAttributeLocation("in_color", 1);
        _defaultShader.BindAttributeLocation("in_uv", 2);
        _defaultShader.BindAttributeLocation("in_clipRect", 3);
    }

    public void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        RenderCommand<Material>[] commands = _reefContext.Builder.Build();

        _instances.Clear();
        for (var i = 0; i < commands.Length; i++)
        {
            RenderCommand<Material> command = commands[i];
            
            if (!_instances.TryGetValue(command, out InstanceVertexData instance))
            {
                instance = new InstanceVertexData(new Vertices(), null);
                _instances.TryAdd(command, instance);
            }

            //  Build rect vertices
            //  Only use the foreground Color if this isn't text
            instance.Rect.AddVertexData(command.Rect, command.Text == null ? command.Color : command.BackgroundColor, command.ClipRect, _reefContext.Builder.Width, _reefContext.Builder.Height);
            
            if (command.Text == null)
            {
                continue;
            }
            
            //  Build text vertices
            if (instance.Text == null)
            {
                instance = new InstanceVertexData(instance.Rect, new Vertices());
                _instances[command] = instance;
            }
            
            TextLayout textLayout = _reefContext.TextEngine.Layout(command.FontOptions, command.Text, command.Rect.Size.X);
            
            for (var n = 0; n < textLayout.Glyphs.Length; n++)
            {
                GlyphLayout glyph = textLayout.Glyphs[n];
                
                var bbox = new IntRect(
                    command.Rect.Left + glyph.BBOX.Left,
                    command.Rect.Top + glyph.BBOX.Top,
                    command.Rect.Left + glyph.BBOX.Right,
                    command.Rect.Top + glyph.BBOX.Bottom
                );
                
                instance.Text!.Value.AddVertexData(bbox, glyph.UV, command.Color, command.ClipRect, _reefContext.Builder.Width, _reefContext.Builder.Height);
            }
        }
    }

    public int Render(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        if (_vao == null)
        {
            return 0;
        }
        
        var drawCalls = 0;
        _vao.Bind();
        foreach (KeyValuePair<RenderCommand<Material>, InstanceVertexData> instance in _instances)
        {
            RenderCommand<Material> command = instance.Key;
            drawCalls += Draw(_vao, command, vertices: instance.Value.Rect);

            if (instance.Value.Text == null)
            {
                continue;
            }
            
            drawCalls += Draw(_vao, command, vertices: instance.Value.Text.Value, isTextPass: true);
        }
        
        return drawCalls;
    }

    private int Draw(VertexArrayObject<float> vao, RenderCommand<Material> command, Vertices vertices, bool isTextPass = false)
    {
        if (vertices.Count == 0)
        {
            return 0;
        }

        if (isTextPass)
        {
            string typefaceID = command.FontOptions.ID ?? _reefContext.TextEngine.GetDefaultTypeface().ID;
            if (!_typefaceMaterials.TryGetValue(typefaceID, out Material? typefaceMaterial))
            {
                //  No matching typeface, and no default is loaded
                return 0;
            }
            
            GLMaterial typefaceGLMaterial = _renderContext.BindMaterial(typefaceMaterial);
            typefaceGLMaterial.Use();
        }
        else if (command.RendererData != null)
        {
            GLMaterial glMaterial = _renderContext.BindMaterial(command.RendererData);
            glMaterial.Use();
        }
        else
        {
            _defaultShader.Activate();
        }

        vao.VertexBufferObject.UpdateData(CollectionsMarshal.AsSpan(vertices.Data));
        _gl.Set(EnableCap.DepthTest, false);
        _gl.PolygonMode(TriangleFace.FrontAndBack, _renderSettings.Wireframe ? PolygonMode.Line : PolygonMode.Fill);
        _gl.MultiDrawArrays(PrimitiveType.TriangleFan, CollectionsMarshal.AsSpan(vertices.Offsets), CollectionsMarshal.AsSpan(vertices.Counts), (uint)vertices.Count);
        return 1;
    }
    
    private readonly struct InstanceVertexData(Vertices rect, Vertices? text)
    {
        public readonly Vertices Rect = rect;
        public readonly Vertices? Text = text;
    }
    
    private readonly struct Vertices()
    {
        public readonly List<int> Offsets = [];
        public readonly List<uint> Counts = [];
        public readonly List<float> Data = [];

        public int Count => Counts.Count;

        public void AddVertexData(IntRect rect, Vector4 color, IntRect clipRect, float width, float height)
        {
            AddVertexData(rect, new IntRect(0, 1, 1, 0), color, clipRect, width, height);
        }

        public void AddVertexData(IntRect rect, IntRect uv, Vector4 color, IntRect clipRect, float width, float height)
        {
            Offsets.Add(Count * 4);
            Counts.Add(4);

            Data.Capacity += 36;

            //  Bottom left
            // X,Y,Z
            Data.Add(MathS.RangeToRange(rect.Left, 0f, width, -1f, 1f));
            Data.Add(MathS.RangeToRange(rect.Bottom, height, 0f, -1f, 1f));
            Data.Add(0f);
            //  r,g,b,a
            Data.Add(color.X);
            Data.Add(color.Y);
            Data.Add(color.Z);
            Data.Add(color.W);
            // u,v
            Data.Add(uv.Left);
            Data.Add(uv.Bottom);
            Data.Add(0);
            //  TODO clip rect should not be baked into every vertex. 
            //       It should be passed per instance via a buffer. This is less straight forward in
            //       the current GLSL 3.30, but doable. It would also require breaking batches into multiple draw
            //       calls if they have too many instances (256 instances per draw call likely is ideal).
            //       -
            //       Alternatively, gl_DrawID is available in GLSL 4.60 and simplifies passing per instance.
            //       This existing behavior could be used anytime using GLSL <4.60.
            //  clip l,t,r,b / x1,y1,x2,y2
            Data.Add(clipRect.Left);
            Data.Add(clipRect.Top);
            Data.Add(clipRect.Right);
            Data.Add(clipRect.Bottom);
            
            //  Bottom right
            // X,Y,Z
            Data.Add(MathS.RangeToRange(rect.Right, 0f, width, -1f, 1f));
            Data.Add(MathS.RangeToRange(rect.Bottom, height, 0f, -1f, 1f));
            Data.Add(0f);
            //  r,g,b,a
            Data.Add(color.X);
            Data.Add(color.Y);
            Data.Add(color.Z);
            Data.Add(color.W);
            // u,v
            Data.Add(uv.Right);
            Data.Add(uv.Bottom);
            Data.Add(0);
            //  clip l,t,r,b / x1,y1,x2,y2
            Data.Add(clipRect.Left);
            Data.Add(clipRect.Top);
            Data.Add(clipRect.Right);
            Data.Add(clipRect.Bottom);
            
            //  Top right
            // X,Y,Z
            Data.Add(MathS.RangeToRange(rect.Right, 0f, width, -1f, 1f));
            Data.Add(MathS.RangeToRange(rect.Top, height, 0f, -1f, 1f));
            Data.Add(0f);
            //  r,g,b,a
            Data.Add(color.X);
            Data.Add(color.Y);
            Data.Add(color.Z);
            Data.Add(color.W);
            // u,v
            Data.Add(uv.Right);
            Data.Add(uv.Top);
            Data.Add(0);
            //  clip l,t,r,b / x1,y1,x2,y2
            Data.Add(clipRect.Left);
            Data.Add(clipRect.Top);
            Data.Add(clipRect.Right);
            Data.Add(clipRect.Bottom);
            
            //  Top left
            // X,Y,Z
            Data.Add(MathS.RangeToRange(rect.Left, 0f, width, -1f, 1f));
            Data.Add(MathS.RangeToRange(rect.Top, height, 0f, -1f, 1f));
            Data.Add(0f);
            //  r,g,b,a
            Data.Add(color.X);
            Data.Add(color.Y);
            Data.Add(color.Z);
            Data.Add(color.W);
            // u,v
            Data.Add(uv.Left);
            Data.Add(uv.Top);
            Data.Add(0);
            //  clip l,t,r,b / x1,y1,x2,y2
            Data.Add(clipRect.Left);
            Data.Add(clipRect.Top);
            Data.Add(clipRect.Right);
            Data.Add(clipRect.Bottom);
        }
    }
    
    private class MaterialRenderCommandComparer : IEqualityComparer<RenderCommand<Material>>
    {
        public bool Equals(RenderCommand<Material> x, RenderCommand<Material> y)
        {
            return x.RendererData == y.RendererData && x.FontOptions.ID == y.FontOptions.ID;
        }

        public int GetHashCode(RenderCommand<Material> obj)
        {
            return HashCode.Combine(obj.RendererData?.GetHashCode() ?? 0, obj.FontOptions.ID?.GetHashCode() ?? 0);
        }
    }
}