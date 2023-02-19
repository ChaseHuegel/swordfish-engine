using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET;

internal static class GLUtil
{
    public static string[] GetExtensions(this GL gl)
    {
        List<string> extensions = new();
        gl.GetInteger(GetPName.NumExtensions, out int count);

        for (uint i = 0; i < count; i++)
            extensions.Add(gl.GetStringS(StringName.Extensions, i));

        return extensions.ToArray();
    }

    public static int GetInt(this GL gl, GetPName getPName)
    {
        gl.GetInteger(getPName, out int value);
        return value;
    }

    public static int GetInt(this GL gl, GLEnum glEnum)
    {
        gl.GetInteger(glEnum, out int value);
        return value;
    }
}
