using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

using Swordfish.Diagnostics;

namespace Swordfish.Rendering
{
    public class Shader
    {
        public readonly string Name;
        public readonly int Handle;

        private readonly Dictionary<string, int> uniformLocations = new Dictionary<string, int>();

        public Shader(string vertex, string fragment, string name = "New Shader")
        {
            Name = name;

            //  Handles
            int VertexShader, FragmentShader;
            string VertexShaderSource, FragmentShaderSource;

            //  Attempt to load vert shader from file, otherwise load from string
            try
            {
                VertexShaderSource = File.ReadAllText(vertex);
            }
            catch
            {
                VertexShaderSource = vertex;
            }

            //  Attempt to load frag shader from file, otherwise load from string
            try
            {
                FragmentShaderSource = File.ReadAllText(fragment);
            }
            catch
            {
                FragmentShaderSource = fragment;
            }

            //  Generate and bind
            VertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(VertexShader, VertexShaderSource);

            FragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(FragmentShader, FragmentShaderSource);

            //  Compile and error checking
            GL.CompileShader(VertexShader);
            string infoLogVert = GL.GetShaderInfoLog(VertexShader);
            if (infoLogVert != System.String.Empty)
                Debug.Log(infoLogVert);

            GL.CompileShader(FragmentShader);
            string infoLogFrag = GL.GetShaderInfoLog(FragmentShader);
            if (infoLogFrag != System.String.Empty)
                Debug.Log(infoLogFrag);

            //  Link to program that can be used
            Handle = GL.CreateProgram();

            GL.AttachShader(Handle, VertexShader);
            GL.AttachShader(Handle, FragmentShader);

            GL.LinkProgram(Handle);

            //  Cleanup
            GL.DetachShader(Handle, VertexShader);
            GL.DetachShader(Handle, FragmentShader);
            GL.DeleteShader(FragmentShader);
            GL.DeleteShader(VertexShader);

            // Get all uniforms
            GL.GetProgram(Handle, GetProgramParameterName.ActiveUniforms, out var numberOfUniforms);
            uniformLocations = new Dictionary<string, int>();

            for (var i = 0; i < numberOfUniforms; i++)
            {
                string key = GL.GetActiveUniform(Handle, i, out _, out _);
                int location = GL.GetUniformLocation(Handle, key);

                uniformLocations.Add(key, location);
            }
        }

        public void Use()
        {
            GL.UseProgram(Handle);
        }

        public void Dispose()
        {
            GL.DeleteProgram(Handle);
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
                    Debug.Log($"The uniform '{uniform}' does not exist in the shader '{Name}'!");
                }
            }

            return location;
        }

        public void SetInt(string name, int data)
        {
            GL.UseProgram(Handle);
            GL.Uniform1(uniformLocations[name], data);
        }

        public void SetFloat(string name, float data)
        {
            GL.UseProgram(Handle);
            GL.Uniform1(uniformLocations[name], data);
        }

        public void SetMatrix4(string name, Matrix4 data)
        {
            GL.UseProgram(Handle);
            GL.UniformMatrix4(uniformLocations[name], true, ref data);
        }

        public void SetMatrix4(string name, Vector3 data)
        {
            GL.UseProgram(Handle);
            GL.Uniform3(uniformLocations[name], data);
        }
    }
}