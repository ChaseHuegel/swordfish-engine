using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

// ReSharper disable once UnusedType.Global
internal sealed class GBuffer : GLHandle
{
    private readonly GL _gl;
    private readonly uint _width;
    private readonly uint _height;
    private readonly uint _positionBuffer;
    private readonly uint _normalBuffer;
    private readonly uint _colorBuffer;
    private readonly uint _depthBuffer;

    public unsafe GBuffer(GL gl,  uint width, uint height)
    {
        _gl = gl;
        _width = width;
        _height = height;

        Bind();
        
        //  Attach color buffers
        _positionBuffer = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _positionBuffer);
        _gl.TexImage2D(GLEnum.Texture2D, 0, InternalFormat.Rgba16f, width, height, 0, GLEnum.Rgba, GLEnum.Float, null);
        _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)TextureMagFilter.Nearest);
        _gl.FramebufferTexture2D(GLEnum.Framebuffer, FramebufferAttachment.ColorAttachment0, GLEnum.Texture2D, _positionBuffer, 0);
        
        _normalBuffer = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _normalBuffer);
        _gl.TexImage2D(GLEnum.Texture2D, 0, InternalFormat.Rgba16f, width, height, 0, GLEnum.Rgba, GLEnum.Float, null);
        _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)TextureMagFilter.Nearest);
        _gl.FramebufferTexture2D(GLEnum.Framebuffer, FramebufferAttachment.ColorAttachment1, GLEnum.Texture2D, _normalBuffer, 0);        
        
        _colorBuffer = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _colorBuffer);
        _gl.TexImage2D(GLEnum.Texture2D, 0, InternalFormat.Rgba, width, height, 0, GLEnum.Rgba, GLEnum.UnsignedByte, null);
        _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)TextureMagFilter.Nearest);
        _gl.FramebufferTexture2D(GLEnum.Framebuffer, FramebufferAttachment.ColorAttachment2, GLEnum.Texture2D, _colorBuffer, 0);
        
        _gl.DrawBuffers(3, [GLEnum.ColorAttachment0, GLEnum.ColorAttachment1, GLEnum.ColorAttachment2]);
        
        //  Attach depth buffer
        _depthBuffer = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(GLEnum.Renderbuffer, _depthBuffer);
        _gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent, width, height);
        _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, FramebufferAttachment.DepthAttachment, GLEnum.Renderbuffer, _depthBuffer);

        GLEnum status = _gl.CheckFramebufferStatus(GLEnum.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new InvalidOperationException();
        }

        Unbind();
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
        _gl.Viewport(0, 0, _width, _height);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);
    }

    protected override void UnbindHandle()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Activate()
    {
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _positionBuffer);
        
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, _normalBuffer);
        
        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, _colorBuffer);
    }
}
