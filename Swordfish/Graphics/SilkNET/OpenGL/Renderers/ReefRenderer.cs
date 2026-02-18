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
using Swordfish.UI.Reef;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class ReefRenderer : IScreenSpaceRenderStage
{
    private readonly GL _gl;
    private readonly GLRenderContext _glRenderContext;
    private readonly RenderSettings _renderSettings;
    private readonly ReefContext _reefContext;
    private readonly DebugSettings _debugSettings;
    private readonly Dictionary<string, Material> _typefaceMaterials = [];

    //  If either of these is null, the renderer is attempting to render without having initialized.
    private readonly ShaderProgram? _defaultShader;
    private readonly VertexArrayObject<float>? _vao;
    
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

        if (!vfs.TryGetFile(AssetPaths.Shaders.At("ui_reef.glsl"), out PathInfo defaultUIShaderFile))
        {
            logger.LogError("The shader source for OpenGL Reef UI was not found. Reef UI will not be rendered with OpenGL.");
            return;
        }

        if (!fileParseService.TryParse(defaultUIShaderFile, out Shader shader))
        {
            logger.LogError("Failed to parse the OpenGL Reef UI shader. Reef UI will not be rendered with OpenGL.");
            return;
        }
        
        if (!vfs.TryGetFile(AssetPaths.Shaders.At("ui_reef_msdf.glsl"), out PathInfo textShaderFile))
        {
            logger.LogError("The shader source for OpenGL Reef UI MSDF text was not found. Reef UI will not be rendered with OpenGL.");
            return;
        }
        
        if (!fileParseService.TryParse(textShaderFile, out Shader textShader))
        {
            logger.LogError("Failed to parse the OpenGL Reef UI MSDF text shader. Reef UI will not be rendered with OpenGL.");
            return;
        }

        foreach (ITypeface typeface in _reefContext.TextEngine.GetTypefaces())
        {
            AtlasInfo atlasInfo = typeface.GetAtlasInfo();
            if (!fileParseService.TryParse(atlasInfo.Path, out Texture atlas))
            {
                logger.LogError("Failed to load the texture for typeface (\"{ID}\") atlas \"{Path}\". This typeface will not render correctly.", typeface.ID, atlasInfo.Path);
                continue;
            }

            if (_typefaceMaterials.ContainsKey(typeface.ID))
            {
                logger.LogWarning("Attempted to add a duplicate of typeface \"{ID}\".", typeface.ID);
                continue;
            }

            var material = new Material(textShader, atlas);
            _typefaceMaterials.Add(typeface.ID, material);
        }

        _vao = glContext.CreateVertexArrayObject(Array.Empty<float>());
        _vao.Bind();
        _vao.VertexBufferObject.Bind();
        _vao.SetVertexAttribute(0, 3, VertexAttribPointerType.Float, 14, 0);
        _vao.SetVertexAttribute(1, 4, VertexAttribPointerType.Float, 14, 3);
        _vao.SetVertexAttribute(2, 3, VertexAttribPointerType.Float, 14, 7);
        _vao.SetVertexAttribute(3, 4, VertexAttribPointerType.Float, 14, 10);
        _vao.VertexBufferObject.Unbind();
        _vao.Unbind();

        _defaultShader = shader.CreateProgram(glContext);
    }

    public void PreRender(double delta, RenderScene renderScene, bool isDepthPass)
    {
        if (isDepthPass)
        {
            return;
        }

        RenderCommand<Material>[] commands = _reefContext.Builder.Build(delta);
        _reefContext.Builder.Debug = _debugSettings.UI.Get();

        _instances.Clear();
        for (var i = 0; i < commands.Length; i++)
        {
            RenderCommand<Material> command = commands[i];

            if (!_instances.TryGetValue(command, out InstanceVertexData instance))
            {
                instance = new InstanceVertexData(new Vertices(), command.Text != null ? new Vertices() : null);
            }

            //  Build rect vertices
            //  Only use the foreground Color if this isn't text
            instance.Rect.AddVertexData(command.Rect, command.Text == null ? command.Color : command.BackgroundColor, command.ClipRect, _reefContext.Builder.Width, _reefContext.Builder.Height);
            
            if (command.Text != null)
            {
                //  Build text vertices
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
                    
                    if (!command.ClipRect.Intersects(bbox))
                    {
                        continue;
                    }
                
                    instance.Text!.Value.AddVertexData(bbox, glyph.UV, command.Color, command.ClipRect, _reefContext.Builder.Width, _reefContext.Builder.Height);
                }
            }
            
            _instances[command] = instance;
        }
    }

    public int Render(double delta, RenderScene renderScene, Action<ShaderProgram> shaderActivationCallback, bool isDepthPass)
    {
        if (_vao == null || isDepthPass)
        {
            return 0;
        }
        
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        
        var drawCalls = 0;
        _vao.Bind();
        foreach (KeyValuePair<RenderCommand<Material>, InstanceVertexData> instance in _instances)
        {
            RenderCommand<Material> command = instance.Key;
            drawCalls += Draw(_vao, command, vertices: instance.Value.Rect, shaderActivationCallback);

            if (instance.Value.Text == null)
            {
                continue;
            }
            
            drawCalls += Draw(_vao, command, vertices: instance.Value.Text.Value, shaderActivationCallback, isTextPass: true);
        }
        
        _gl.Disable(EnableCap.Blend);
        return drawCalls;
    }

    private int Draw(VertexArrayObject<float> vao, RenderCommand<Material> command, Vertices vertices, Action<ShaderProgram> shaderActivationCallback, bool isTextPass = false)
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
            
            GLMaterial typefaceGLMaterial = _glRenderContext.BindMaterial(typefaceMaterial);
            
            using GLMaterial.Scope _ = typefaceGLMaterial.Use();
            shaderActivationCallback(typefaceGLMaterial.ShaderProgram);
            
            DrawArrays(vao, vertices);
        }
        else if (command.RendererData != null)
        {
            GLMaterial glMaterial = _glRenderContext.BindMaterial(command.RendererData);
            
            using GLMaterial.Scope _ = glMaterial.Use();
            shaderActivationCallback(glMaterial.ShaderProgram);
            
            DrawArrays(vao, vertices);
        }
        else
        {
            if (_defaultShader == null)
            {
                return 0;
            }
            
            using GLHandle.Scope _ = _defaultShader.Use();
            shaderActivationCallback(_defaultShader);
            var viewport = new int[4];
            _gl.GetInteger(GLEnum.Viewport, viewport);
            _defaultShader.SetUniform("screenSize", new Vector2(viewport[2], viewport[3]));
            
            DrawArrays(vao, vertices);
        }

        return 1;
    }

    private void DrawArrays(VertexArrayObject<float> vao, Vertices vertices)
    {
        vao.VertexBufferObject.UpdateData(CollectionsMarshal.AsSpan(vertices.Data));
        _gl.Set(EnableCap.DepthTest, false);
        _gl.PolygonMode(TriangleFace.FrontAndBack, _renderSettings.Wireframe ? PolygonMode.Line : PolygonMode.Fill);
        _gl.MultiDrawArrays(PrimitiveType.TriangleFan, CollectionsMarshal.AsSpan(vertices.Offsets), CollectionsMarshal.AsSpan(vertices.Counts), (uint)vertices.Count);
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
            Offsets.Add(Data.Count / 14);
            Counts.Add(4);

            Data.Capacity += 56;

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