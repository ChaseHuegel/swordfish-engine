using Silk.NET.Core.Contexts;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class FramebufferObject : GLHandle
{
    public string Name { get; }
    
    private readonly GL _gl;
    private readonly TexImage2D _texture;

    public FramebufferObject(GL gl, string name, TexImage2D texture, FramebufferAttachment attachment)
    {
        _gl = gl;
        Name = name;
        _texture = texture;

        using Scope _ = Use();
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment, textarget: TextureTarget.Texture2D, texture.Handle, level: 0);
        
        GLEnum status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new FatalAlertException($"Framebuffer \"{name}\" is incomplete.");
        }
    }

    protected override uint CreateHandle()
    {
        return _gl.GenFramebuffer();
    }

    protected override void FreeHandle()
    {
        _gl.DeleteFramebuffer(Handle);
    }

    protected override void BindHandle()
    {
        _gl.Viewport(0, 0, _texture.Width, _texture.Height);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);
    }

    protected override void UnbindHandle()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public FramebufferScope Use(FramebufferTarget target)
    {
        return new FramebufferScope(_gl, fbo: this, target);
    }
    
    public FramebufferScope Use(FramebufferTarget target, DrawBufferMode mode)
    {
        return new FramebufferScope(_gl, fbo: this, target, mode);
    }
    
    public FramebufferScope Use(FramebufferTarget target, ReadBufferMode mode)
    {
        return new FramebufferScope(_gl, fbo: this, target, mode);
    }
    
    public readonly struct FramebufferScope : IDisposable
    {
        private readonly GL _gl;
        private readonly FramebufferTarget _target;

        public Scope(in GL gl, in FramebufferObject fbo, FramebufferTarget target)
        {
            _gl = gl;
            _target = target;
            gl.BindFramebuffer(target, fbo.Handle);
        }
        
        public Scope(in GL gl, in FramebufferObject fbo, FramebufferTarget target, DrawBufferMode mode) 
            : this(gl, fbo, target)
        {
            gl.DrawBuffer(mode);
        }
        
        public Scope(in GL gl, in FramebufferObject fbo, FramebufferTarget target, ReadBufferMode mode)
            : this(gl, fbo, target)
        {
            gl.ReadBuffer(mode);
        }

        public void Dispose()
        {
            _gl.BindFramebuffer(_target, 0);
        }
    }
}
