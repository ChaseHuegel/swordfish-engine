using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Swordfish.Graphics.SilkNET.OpenGL;

namespace Swordfish.Diagnostics.SilkNET.OpenGL;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class GLDebug : IDisposable, IAutoActivate
{
    private readonly GL _gl;
    private readonly ILogger _logger;
    private readonly IWindow _window;

    public GLDebug(in GL gl, in ILogger logger, in IWindow window)
    {
        _gl = gl;
        _logger = logger;
        _window = window;
        
        if (_gl.HasCapabilities(4, 3, "GL_KHR_debug"))
        {
            _gl.DebugMessageCallback(DebugCallback, IntPtr.Zero);
            _gl.Enable(EnableCap.DebugOutput);
            _gl.Enable(EnableCap.DebugOutputSynchronous);
        }
        else
        {
            _logger.LogWarning("OpenGL debug output is unsupported, logs for OpenGL will be minimal and generic.");
            _window.Render += OnRender;
        }
    }
    
    public void Dispose()
    {
        _window.Render -= OnRender;
    }

    private void OnRender(double delta)
    {
        GLEnum error = _gl.GetError();
        while (error != GLEnum.NoError)
        {
            _logger.LogError("[OpenGL] {output}", error.ToString());
            error = _gl.GetError();
        }
    }

    private void DebugCallback(GLEnum source, GLEnum type, int id, GLEnum severity, int length, nint message, nint userParam)
    {
        string output = Marshal.PtrToStringAnsi(message, length);
        if (type == GLEnum.DebugTypeError)
        {
            _logger.LogError("[OpenGL] {output}", output);
        }
        else
        {
            _logger.LogTrace("[OpenGL] {output}", output);
        }
    }
}