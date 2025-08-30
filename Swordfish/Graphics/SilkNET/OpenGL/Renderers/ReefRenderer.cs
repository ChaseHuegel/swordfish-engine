using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Reef;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Util;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.Settings;
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
    in ILogger logger) : IRenderStage
{
    private readonly GL _gl = gl;
    private readonly GLContext _glContext = glContext;
    private readonly RenderSettings _renderSettings = renderSettings;
    private readonly ReefContext _reefContext = reefContext;
    private readonly IFileParseService _fileParseService = fileParseService;
    private readonly VirtualFileSystem _vfs = vfs;
    private readonly ILogger _logger = logger;

    //  If either of these is null, the renderer is attempting to render without having initialized.
    private GLRenderContext _renderContext = null!;
    private ShaderProgram _defaultShader = null!;
    
    private VertexArrayObject<float>? _vao;
    private readonly Dictionary<RenderCommand<Material>, IntRectVertices> _instances = new(new MaterialRenderCommandComparer());

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

        _renderContext = glRenderContext;
        _vao = _glContext.CreateVertexArrayObject(Array.Empty<float>());

        _vao.Bind();
        _vao.VertexBufferObject.Bind();
        _vao.SetVertexAttribute(0, 3, VertexAttribPointerType.Float, 10, 0);
        _vao.SetVertexAttribute(1, 4, VertexAttribPointerType.Float, 10, 3);
        _vao.SetVertexAttribute(2, 3, VertexAttribPointerType.Float, 10, 7);

        _defaultShader = shader.CreateProgram(_glContext);
        _defaultShader.BindAttributeLocation("in_position", 0);
        _defaultShader.BindAttributeLocation("in_color", 1);
        _defaultShader.BindAttributeLocation("in_uv", 2);
    }

    public void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        RenderCommand<Material>[] commands = _reefContext.Builder.Build();

        _instances.Clear();
        for (var i = 0; i < commands.Length; i++)
        {
            RenderCommand<Material> command = commands[i];
            
            if (!_instances.TryGetValue(command, out IntRectVertices vertices))
            {
                vertices = new IntRectVertices();
                _instances.TryAdd(command, vertices);
            }

            vertices.AddVertexData(command.Rect, command.Color, _reefContext.Builder.Width, _reefContext.Builder.Height);
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
        foreach (KeyValuePair<RenderCommand<Material>, IntRectVertices> instance in _instances)
        {
            drawCalls += Draw(_vao, instance.Key, instance.Value);
        }
        
        return drawCalls;
    }

    private int Draw(VertexArrayObject<float> vao, RenderCommand<Material> command, IntRectVertices vertices)
    {
        if (vertices.Count == 0)
        {
            return 0;
        }

        if (command.RendererData != null)
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
    
    private readonly struct IntRectVertices()
    {
        public readonly List<int> Offsets = [];
        public readonly List<uint> Counts = [];
        public readonly List<float> Data = [];

        public int Count => Counts.Count;

        public void AddVertexData(IntRect rect, Vector4 color, float width, float height)
        {
            Offsets.Add(Count * 4);
            Counts.Add(4);

            Data.Capacity += 20;

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
            Data.Add(0);
            Data.Add(1);
            Data.Add(0);
            
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
            Data.Add(1);
            Data.Add(1);
            Data.Add(0);
            
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
            Data.Add(1);
            Data.Add(0);
            Data.Add(0);
  
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
            Data.Add(0);
            Data.Add(0);
            Data.Add(0);
        }
    }
    
    private class MaterialRenderCommandComparer : IEqualityComparer<RenderCommand<Material>>
    {
        public bool Equals(RenderCommand<Material> x, RenderCommand<Material> y)
        {
            return x.RendererData == y.RendererData;
        }

        public int GetHashCode(RenderCommand<Material> obj)
        {
            return obj.RendererData?.GetHashCode() ?? 0;
        }
    }
}