using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class FramebufferObject : GLHandle
{
    public string Name { get; }
    
    private readonly GL _gl;
    private readonly uint _width;
    private readonly uint _height;
    private readonly TexImage2D? _texture;

    public FramebufferObject(GL gl, string name, TexImage2D texture, FramebufferAttachment attachment)
        : this(gl, name, texture.Width, texture.Height)
    {
        _texture = texture;
        
        using Scope _ = Use();
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment, textarget: TextureTarget.Texture2D, texture.Handle, level: 0);
        
        GLEnum status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new FatalAlertException($"Framebuffer \"{name}\" is incomplete.");
        }
    }

    public FramebufferObject(GL gl, string name, uint width, uint height, DrawBufferMode[]? drawBufferModes, RenderbufferObject[]? renderBuffers) 
        : this(gl, name, width, height)
    {
        using Scope _ = Use();
        
        if (drawBufferModes != null)
        {
            gl.DrawBuffers((uint)drawBufferModes.Length, drawBufferModes);
        }
        
        if (renderBuffers != null)
        {
            for (var i = 0; i < renderBuffers.Length; i++)
            {
                RenderbufferObject rbo = renderBuffers[i];
                
                using Scope rboScope = rbo.Use();
                gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, rbo.Attachment, RenderbufferTarget.Renderbuffer, rbo.Handle);
            }
        }
        
        GLEnum status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new FatalAlertException($"Framebuffer \"{name}\" is incomplete.");
        }
    }

    private FramebufferObject(GL gl, string name, uint width, uint height)
    {
        _gl = gl;
        Name = name;
        _width = width;
        _height = height;
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
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);
        _gl.Viewport(0, 0, _width, _height);
    }

    protected override void UnbindHandle()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }
    
    public void Blit(FramebufferObject destination, ReadBufferMode readMode, DrawBufferMode drawMode, ClearBufferMask mask, BlitFramebufferFilter filter)
    {
        using FramebufferScope readScope = Use(FramebufferTarget.ReadFramebuffer, readMode);
        using FramebufferScope drawScope = destination.Use(FramebufferTarget.DrawFramebuffer, drawMode);
        
        if (_texture != null)
        {
            using Scope textureScope = _texture.Use();
        }
        
        _gl.BlitFramebuffer(
            0, 0, (int)_width, (int)_height,
            0, 0, (int)destination._width, (int)destination._height,
            mask,
            filter
        );
    }
    
    public void Blit(ReadBufferMode readMode, DrawBufferMode drawMode, ClearBufferMask mask, BlitFramebufferFilter filter)
    {
        using FramebufferScope readScope = Use(FramebufferTarget.ReadFramebuffer, readMode);
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
        _gl.DrawBuffer(drawMode);
        
        if (_texture != null)
        {
            using Scope textureScope = _texture.Use();
        }
        
        _gl.BlitFramebuffer(
            0, 0, (int)_width, (int)_height,
            0, 0, (int)_width, (int)_height,
            mask,
            filter
        );
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

        public FramebufferScope(in GL gl, in FramebufferObject fbo, FramebufferTarget target)
        {
            _gl = gl;
            _target = target;
            gl.BindFramebuffer(target, fbo.Handle);
        }
        
        public FramebufferScope(in GL gl, in FramebufferObject fbo, FramebufferTarget target, DrawBufferMode mode) 
            : this(gl, fbo, target)
        {
            gl.DrawBuffer(mode);
        }
        
        public FramebufferScope(in GL gl, in FramebufferObject fbo, FramebufferTarget target, ReadBufferMode mode)
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
