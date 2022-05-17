using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

using Swordfish.Library.Diagnostics;

namespace Swordfish.Engine.Rendering
{
    public class Shader
    {
        public readonly string Name;
        public readonly int Handle;

        private readonly Dictionary<string, int> uniformLocations = new Dictionary<string, int>();

        public static Shader LoadFromFile(string vertexPath, string fragmentPath, string name = "New Shader")
        {
            Debug.Log($"Loading shader '{name}' from '{vertexPath}' / '{fragmentPath}'");

            if (!File.Exists(vertexPath))
			{
				Debug.Log($"Unable to load shader '{name}', vertex source not found at '{vertexPath}'", LogType.ERROR);
                return null;
            }

            if (!File.Exists(fragmentPath))
			{
				Debug.Log($"Unable to load shader '{name}', fragment source not found at '{fragmentPath}'", LogType.ERROR);
                return null;
            }

            return new Shader(File.ReadAllText(vertexPath), File.ReadAllText(fragmentPath), name);
        }

        public Shader(string vertexSource, string fragmentSource, string name = "New Shader")
        {
            Name = name;

            //  Handles
            int vertexShader, fragmentShader;

            //  Generate and bind
            vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexSource);

            fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentSource);

            //  Compile and error checking
            GL.CompileShader(vertexShader);
            string infoLogVert = GL.GetShaderInfoLog(vertexShader);
            if (infoLogVert != System.String.Empty)
                Debug.Log(infoLogVert);

            GL.CompileShader(fragmentShader);
            string infoLogFrag = GL.GetShaderInfoLog(fragmentShader);
            if (infoLogFrag != System.String.Empty)
                Debug.Log(infoLogFrag);

            //  Link to program that can be used
            Handle = GL.CreateProgram();

            GL.AttachShader(Handle, vertexShader);
            GL.AttachShader(Handle, fragmentShader);

            GL.LinkProgram(Handle);

            //  Cleanup
            GL.DetachShader(Handle, vertexShader);
            GL.DetachShader(Handle, fragmentShader);
            GL.DeleteShader(fragmentShader);
            GL.DeleteShader(vertexShader);

            // Get all uniforms
            GL.GetProgram(Handle, GetProgramParameterName.ActiveUniforms, out var numberOfUniforms);
            uniformLocations = new Dictionary<string, int>();

            string uniformOutput = "";
            for (int i = 0; i < numberOfUniforms; i++)
            {
                string key = GL.GetActiveUniform(Handle, i, out _, out _);
                int location = GL.GetUniformLocation(Handle, key);
                uniformLocations.Add(key, location);
                uniformOutput += key + (i  == numberOfUniforms-1 ? "" : ", ");
            }

            Debug.Log($"Uniforms: {uniformOutput}", LogType.CONTINUED);
        }

        public void Use()
        {
            GL.UseProgram(Handle);
        }

        public void Dispose()
        {
            GL.DeleteProgram(Handle);
        }

        public bool TryValidateUniform(string name)
        {
            return (GetUniformLocation(name) != -1);
        }

        public int GetAttribLocation(string attribName)
        {
            return GL.GetAttribLocation(Handle, attribName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetUniformLocation(string uniform)
        {
            if (uniformLocations.TryGetValue(uniform, out int location) == false)
            {
                location = GL.GetUniformLocation(Handle, uniform);
                uniformLocations.Add(uniform, location);

                if (location == -1)
                {
                    Debug.Log($"Uniform '{uniform}' does not exist in the shader '{Name}'!", LogType.WARNING, true);
                }
            }

            return location;
        }

        public void SetInt(string name, int data)
        {
            if (!TryValidateUniform(name)) return;
            GL.UseProgram(Handle);
            GL.Uniform1(uniformLocations[name], data);
        }

        public void SetFloat(string name, float data)
        {
            if (!TryValidateUniform(name)) return;
            GL.UseProgram(Handle);
            GL.Uniform1(uniformLocations[name], data);
        }

        public void SetVec2(string name, Vector2 data)
        {
            if (!TryValidateUniform(name)) return;
            GL.UseProgram(Handle);
            GL.Uniform2(uniformLocations[name], data.X, data.Y);
        }

        public void SetVec3(string name, Vector3 data)
        {
            if (!TryValidateUniform(name)) return;
            GL.UseProgram(Handle);
            GL.Uniform3(uniformLocations[name], data.X, data.Y, data.Z);
        }

        public void SetVec4(string name, Vector4 data)
        {
            if (!TryValidateUniform(name)) return;
            GL.UseProgram(Handle);
            GL.Uniform4(uniformLocations[name], data.X, data.Y, data.Z, data.W);
        }

        public void SetMatrix4(string name, Matrix4 data)
        {
            if (!TryValidateUniform(name)) return;
            GL.UseProgram(Handle);
            GL.UniformMatrix4(uniformLocations[name], true, ref data);
        }
    }
}