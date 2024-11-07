using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

// ReSharper disable once UnusedType.Global
internal sealed class FramebufferObject : GLHandle
{
    private readonly GL _gl;
    private readonly uint _width;
    private readonly uint _height;
    private readonly uint _textureHandle;

    public unsafe FramebufferObject(GL gl,  uint width, uint height)
    {
        _gl = gl;
        _width = width;
        _height = height;

        Bind();
        _textureHandle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _textureHandle);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent, _width, _height, 0, PixelFormat.DepthComponent, PixelType.Float, null);

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, [1f, 1f, 1f, 1f]);

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, _textureHandle, 0);

        //  Disable reading/writing the color buffer
        _gl.DrawBuffer(DrawBufferMode.None);
        _gl.ReadBuffer(ReadBufferMode.None);

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

    // ReSharper disable once UnusedMember.Global
    public void Activate()
    {
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, _textureHandle);
    }
}
