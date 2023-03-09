using Silk.NET.OpenGL;
using Swordfish.Library.Extensions;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal unsafe partial class GLContext
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
        return GLThread.WaitForResult(SharderProgramArgs.Create, new SharderProgramArgs(GL, name, vertexSource, fragmentSource));
    }

    public TexImage2D CreateTexImage2D(string name, byte[] pixels, uint width, uint height, bool generateMipmaps = false)
    {
        fixed (byte* pixelPtr = pixels)
        {
            return GLThread.WaitForResult(TextureArgs.Create, new TextureArgs(GL, name, pixelPtr, width, height, generateMipmaps));
        }
    }
}
