using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class FramebufferObject : GLHandle
{
    private readonly GL GL;
    public readonly uint Width;
    public readonly uint Height;
    private readonly uint TextureHandle;

    public unsafe FramebufferObject(GL gl,  uint width, uint height)
    {
        GL = gl;
        Width = width;
        Height = height;

        Bind();
        TextureHandle = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, TextureHandle);
        GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent, Width, Height, 0, PixelFormat.DepthComponent, PixelType.Float, null);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 1f, 1f, 1f, 1f } );

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, TextureHandle, 0);

        //  Disable reading/writing the color buffer
        GL.DrawBuffer(DrawBufferMode.None);
        GL.ReadBuffer(ReadBufferMode.None);

        Unbind();
    }

    protected override uint CreateHandle()
    {
        return GL.GenFramebuffer();
    }

    protected override void FreeHandle()
    {
        GL.DeleteFramebuffer(Handle);
    }

    protected override void BindHandle()
    {
        GL.Viewport(0, 0, Width, Height);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);
    }

    protected override void UnbindHandle()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Activate()
    {
        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, TextureHandle);
    }
}
