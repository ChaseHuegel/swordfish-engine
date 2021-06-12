using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Threading;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;

namespace Swordfish.Rendering
{
    public class Shader
    {
        public readonly string Name;
        public readonly int Handle;

        private readonly Dictionary<string, int> uniformLocations = new Dictionary<string, int>();

        public Shader(string vertexPath, string fragmentPath, string name = "New Shader")
        {
            Name = name;

            //  Handles
            int VertexShader, FragmentShader;

            //  Attempt to load vert shader from file, otherwise load from string
            string VertexShaderSource;
            try
            {
                using (StreamReader reader = new StreamReader(vertexPath, Encoding.UTF8))
                {
                    VertexShaderSource = reader.ReadToEnd();
                }
            }
            catch
            {
                VertexShaderSource = vertexPath;
            }

            //  Attempt to load frag shader from file, otherwise load from string
            string FragmentShaderSource;
            try
            {
                using (StreamReader reader = new StreamReader(fragmentPath, Encoding.UTF8))
                {
                    FragmentShaderSource = reader.ReadToEnd();
                }
            }
            catch
            {
                FragmentShaderSource = fragmentPath;
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