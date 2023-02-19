using Silk.NET.OpenGL;
using Swordfish.Library.Extensions;

namespace Swordfish.Graphics.SilkNET;

public class GLContext
{
    private readonly GL GL;
    private readonly SynchronizationContext GLThread;

    public GLContext(GL gl, SynchronizationContext synchronizationContext)
    {
        GL = gl;
        GLThread = synchronizationContext;
    }

    public ShaderProgram CreateShaderProgram(string name, string vertexSource, string fragmentSource)
    {
        return GLThread.WaitForResult(() => new ShaderProgram(GL, name, vertexSource, fragmentSource));
    }
}
