using System.Runtime.Serialization;

namespace Swordfish.Graphics.SilkNET.OpenGL;

[Serializable]
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

    protected GLException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}