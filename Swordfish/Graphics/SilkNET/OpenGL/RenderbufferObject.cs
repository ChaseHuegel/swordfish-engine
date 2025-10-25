using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class RenderbufferObject : GLHandle
{
    public string Name { get; }
    public FramebufferAttachment Attachment { get; }
    
    private readonly GL _gl;
    private readonly uint? _samples;
    private readonly InternalFormat _format;
    
    public RenderbufferObject(GL gl, string name, uint width, uint height, FramebufferAttachment attachment, InternalFormat format, uint? samples) 
    {
        _gl = gl;
        Name = name;
        Attachment = attachment;
        _format = format;
        _samples = samples;
        
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
    
    public void Resize(uint width, uint height)
    {
        using Scope _ = Use();
        if (_samples != null)
        {
            _gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, _samples.Value, _format, width, height);
        }
        else
        {
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, _format, width, height);
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
