using System.Numerics;
using Shoal.DependencyInjection;
using Silk.NET.OpenGL;
using Swordfish.Library.Extensions;
using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class GLRenderer : IRenderer, IDisposable, IAutoActivate
{
    public DataBinding<int> DrawCalls { get; } = new();

    private readonly GL _gl;
    private readonly IWindowContext _windowContext;
    private readonly IRenderPipeline[] _renderPipelines;
    private readonly IRenderContext _renderContext;
    private readonly SynchronizationContext _synchronizationContext;

    public GLRenderer(
        in GL gl,
        in IWindowContext windowContext,
        in IRenderPipeline[] renderPipelines,
        in IRenderContext renderContext,
        in SynchronizationContext synchronizationContext
    ) {
        _gl = gl;
        _windowContext = windowContext;
        _renderPipelines = renderPipelines;
        _renderContext = renderContext;
        _synchronizationContext = synchronizationContext;

        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.CullFace);
        gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        
        _windowContext.Render += OnWindowRender;
    }
    
    public void Dispose()
    {
        _windowContext.Render -= OnWindowRender;
    }

    public Texture Screenshot()
    {
        (byte[] Pixels, int Width, int Height, DateTime Time) screenshot = _synchronizationContext.WaitForResult(ScreenshotCallback);
        unsafe (byte[] Pixels, int Width, int Height, DateTime Time) ScreenshotCallback()
        {
            Vector2 size = _windowContext.GetSize();
            var width = (uint)size.X;
            var height = (uint)size.Y;
            DateTime time = DateTime.Now;
         
            var pixels = new byte[width * height * 3];
            fixed (byte* p = pixels)
            {
                _gl.GetInteger(GetPName.PackAlignment, out int previousPackAlignment);
                _gl.PixelStore(PixelStoreParameter.PackAlignment, 1);

                _gl.ReadPixels(0, 0, width, height, PixelFormat.Rgb, PixelType.UnsignedByte, p);
                
                _gl.PixelStore(PixelStoreParameter.PackAlignment, previousPackAlignment);
            }

            return (pixels, (int)width, (int)height, time);
        }
        
        //  Intentionally not performing this post-processing in the synchronization context 
        var flippedPixels = new byte[screenshot.Pixels.Length];
        int stride = screenshot.Width * 3;
        for (var y = 0; y < screenshot.Height; y++)
        {
            Array.Copy(
                sourceArray: screenshot.Pixels,
                sourceIndex: y * stride,
                destinationArray: flippedPixels,
                destinationIndex: (screenshot.Height - 1 - y) * stride,
                length: stride
            );
        }
        
        return new Texture(name: $"{screenshot.Time:yy-MM-dd_HH.mm.ss}", flippedPixels, screenshot.Width, screenshot.Height, mipmaps: false);
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