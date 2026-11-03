using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

/// <summary>Two render targets that alternate between source and destination.</summary>
/// <remarks>A pass reads <see cref="Front"/> and draws to <see cref="BackTarget"/>, then calls <see cref="Swap"/>.</remarks>
internal sealed unsafe class DoubleBufferTarget : IDisposable
{
    public TexImage2D Front { get; private set; }
    public TexImage2D Back { get; private set; }

    public FramebufferObject FrontTarget { get; private set; }
    public FramebufferObject BackTarget { get; private set; }

    public DoubleBufferTarget(GL gl, string name, uint width, uint height, TextureFormat format, TextureParams @params)
    {
        Front = new TexImage2D(gl, name: $"{name}_a", pixels: null, width, height, format, @params);
        Back = new TexImage2D(gl, name: $"{name}_b", pixels: null, width, height, format, @params);

        FrontTarget = new FramebufferObject(gl, name: $"{name}_a", Front, FramebufferAttachment.ColorAttachment0);
        BackTarget = new FramebufferObject(gl, name: $"{name}_b", Back, FramebufferAttachment.ColorAttachment0);
    }

    /// <summary>Makes the previous destination the new source.</summary>
    public void Swap()
    {
        (Front, Back) = (Back, Front);
        (FrontTarget, BackTarget) = (BackTarget, FrontTarget);
    }

    public void Resize(uint width, uint height)
    {
        FrontTarget.Resize(width, height);
        BackTarget.Resize(width, height);
    }

    public void Dispose()
    {
        FrontTarget.Dispose();
        BackTarget.Dispose();
        Front.Dispose();
        Back.Dispose();
    }
}
