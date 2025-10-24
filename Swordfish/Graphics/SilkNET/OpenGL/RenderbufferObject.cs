using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class RenderbufferObject : GLHandle
{
    public string Name { get; }
    public FramebufferAttachment Attachment { get; }
    
    private readonly GL _gl;
    
    public RenderbufferObject(GL gl, string name, uint width, uint height, FramebufferAttachment attachment, InternalFormat format, uint? samples) 
    {
        _gl = gl;
        Name = name;
        Attachment = attachment;
        
        using Scope _ = Use();
        
        if (samples != null)
        {
            gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, samples.Value, format, width, height);
        }
        else
        {
            gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, format, width, height);
        }
        
        GLEnum status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new FatalAlertException($"Framebuffer \"{name}\" is incomplete.");
        }
    }

    protected override uint CreateHandle()
    {
        return _gl.GenRenderbuffer();
    }

    protected override void FreeHandle()
    {
        _gl.DeleteRenderbuffer(Handle);
    }

    protected override void BindHandle()
    {
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, Handle);
    }

    protected override void UnbindHandle()
    {
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
    }
}
