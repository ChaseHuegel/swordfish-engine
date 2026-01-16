using Shoal.DependencyInjection;
using Silk.NET.OpenGL;
using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class GLRenderer : IRenderer, IDisposable, IAutoActivate
{
    public DataBinding<int> DrawCalls { get; } = new();

    private readonly GL _gl;
    private readonly IWindowContext _windowContext;
    private readonly IRenderPipeline[] _renderPipelines;
    private readonly IRenderContext _renderContext;

    public GLRenderer(
        in GL gl,
        in IWindowContext windowContext,
        in IRenderPipeline[] renderPipelines,
        in IRenderContext renderContext
    ) {
        _gl = gl;
        _windowContext = windowContext;
        _renderPipelines = renderPipelines;
        _renderContext = renderContext;

        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.CullFace);
        gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        
        _windowContext.Render += OnWindowRender;
    }
    
    public void Dispose()
    {
        _windowContext.Render -= OnWindowRender;
    }

    private void OnWindowRender(double delta)
    {
        RenderScene renderScene = _renderContext.GetSceneContext();
        
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        
        var drawCalls = 0;
        for (var i = 0; i < _renderPipelines.Length; i++)
        {
            drawCalls += _renderPipelines[i].Render(delta, renderScene);
        }
        
        DrawCalls.Set(drawCalls);
    }
}