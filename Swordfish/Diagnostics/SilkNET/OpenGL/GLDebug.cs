using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Silk.NET.OpenGL;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;

namespace Swordfish.Diagnostics.SilkNET.OpenGL;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class GLDebug : IDisposable, IAutoActivate
{
    private readonly GL _gl;
    private readonly ILogger _logger;
    private readonly IWindowContext _windowContext;

    public GLDebug(in GL gl, in ILogger logger, in IWindowContext windowContext)
    {
        _gl = gl;
        _logger = logger;
        _windowContext = windowContext;
        
        if (_gl.HasCapabilities(4, 3, "GL_KHR_debug"))
        {
            _gl.DebugMessageCallback(DebugCallback, IntPtr.Zero);
            _gl.Enable(EnableCap.DebugOutput);
            _gl.Enable(EnableCap.DebugOutputSynchronous);
        }
        else
        {
            _logger.LogWarning("OpenGL debug output is unsupported, logs for OpenGL will be minimal and generic.");
            _windowContext.Render += OnRender;
        }
    }
    
    public void Dispose()
    {
        _windowContext.Render -= OnRender;
    }
    
    public bool TryLogError() 
    {
        GLEnum error = _gl.GetError();
        if (error == GLEnum.NoError)
        {
            return false;
        }

        _logger.LogError("[OpenGL] {output} {stack}", error.ToString(), new StackTrace(fNeedFileInfo: true));
        return true;

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