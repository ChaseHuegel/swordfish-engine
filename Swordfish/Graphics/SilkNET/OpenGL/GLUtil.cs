using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal static class GLUtil
{
    public static bool HasCapabilities(this GL gl, int major, int minor, params string[] extensions)
    {
        string versionString = gl.GetStringS(StringName.Version);
        Version version = new(versionString.Split(' ')[0]);

        return version >= new Version(major, minor) || gl.HasExtensions(extensions);
    }

    public static bool HasExtensions(this GL gl, params string[] extensions)
    {
        string[] supportedExtensions = gl.GetExtensions();

        foreach (string extension in extensions)
            if (!supportedExtensions.Contains(extension))
                return false;

        return true;
    }

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

    public static void Set(this GL gl, EnableCap enableCap, bool value)
    {
        if (value)
        {
            gl.Enable(enableCap);
            return;
        }

        gl.Disable(enableCap);
    }
}
