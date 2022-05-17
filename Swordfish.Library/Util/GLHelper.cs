using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;

namespace Swordfish.Library.Util
{
    public static class GLHelper
    {
        public static void SetProperty(EnableCap property, bool value)
        {
            if (value)
                GL.Disable(EnableCap.CullFace);
            else
                GL.Enable(EnableCap.CullFace);
        }

        public static MonitorInfo GetPrimaryDisplay() => OpenTK.Windowing.Desktop.Monitors.GetPrimaryMonitor();
        public static MonitorInfo GetDisplay(int index) => OpenTK.Windowing.Desktop.Monitors.GetMonitors()[0];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasCapabilities(int major, int minor, params string[] extensions)
        {
            string versionString = GL.GetString(StringName.Version);
            Version version = new Version(versionString.Split(' ')[0]);

            return version >= new Version(major, minor) || HasExtensions(extensions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasExtensions(params string[] extensions)
        {
            List<string> supportedExtensions = GetSupportedExtensions();

            foreach (var extension in extensions)
                if (!supportedExtensions.Contains(extension))
                    return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<string> GetSupportedExtensions()
        {
            List<string> extensions = new List<string>();
            GL.GetInteger(GetPName.NumExtensions, out int count);

            for (int i = 0; i < count; i++)
                extensions.Add(GL.GetString(StringNameIndexed.Extensions, i));

            return extensions;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LabelObject(ObjectLabelIdentifier objLabelIdent, int glObject, string name)
        {
            GL.ObjectLabel(objLabelIdent, glObject, name.Length, name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateTexture(TextureTarget target, string Name, out int Texture)
        {
            GL.CreateTextures(target, 1, out Texture);
            LabelObject(ObjectLabelIdentifier.Texture, Texture, $"Texture: {Name}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateProgram(string Name, out int Program)
        {
            Program = GL.CreateProgram();
            LabelObject(ObjectLabelIdentifier.Program, Program, $"Program: {Name}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateShader(ShaderType type, string Name, out int Shader)
        {
            Shader = GL.CreateShader(type);
            LabelObject(ObjectLabelIdentifier.Shader, Shader, $"Shader: {type}: {Name}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateBuffer(string Name, out int Buffer)
        {
            GL.CreateBuffers(1, out Buffer);
            LabelObject(ObjectLabelIdentifier.Buffer, Buffer, $"Buffer: {Name}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateVertexBuffer(string Name, out int Buffer) => CreateBuffer($"VBO: {Name}", out Buffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateElementBuffer(string Name, out int Buffer) => CreateBuffer($"EBO: {Name}", out Buffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateVertexArray(string Name, out int VAO)
        {
            GL.CreateVertexArrays(1, out VAO);
            LabelObject(ObjectLabelIdentifier.VertexArray, VAO, $"VAO: {Name}");
        }
    }
}