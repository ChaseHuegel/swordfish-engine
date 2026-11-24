namespace Swordfish.Graphics.SilkNET.OpenGL;

internal class GLException : Exception
{
    public GLException()
    {
    }

    public GLException(string? message) : base(message)
    {
    }

    public GLException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}