using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Threading;
using OpenTK.Graphics.OpenGL4;

namespace Swordfish.Rendering
{
    public class Shader
    {
        public readonly string Name;
        public int Handle { get { return handle; } }
        private int handle;

        private readonly Dictionary<string, int> UniformToLocation = new Dictionary<string, int>();

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
            handle = GL.CreateProgram();

            GL.AttachShader(handle, VertexShader);
            GL.AttachShader(handle, FragmentShader);

            GL.LinkProgram(handle);

            //  Cleanup
            GL.DetachShader(handle, VertexShader);
            GL.DetachShader(handle, FragmentShader);
            GL.DeleteShader(FragmentShader);
            GL.DeleteShader(VertexShader);
        }

        public void Use()
        {
            GL.UseProgram(handle);
        }

        public void Dispose()
        {
            GL.DeleteProgram(handle);
        }

        public int GetAttribLocation(string attribName)
        {
            return GL.GetAttribLocation(handle, attribName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetUniformLocation(string uniform)
        {
            if (UniformToLocation.TryGetValue(uniform, out int location) == false)
            {
                location = GL.GetUniformLocation(handle, uniform);
                UniformToLocation.Add(uniform, location);

                if (location == -1)
                {
                    Debug.Log($"The uniform '{uniform}' does not exist in the shader '{Name}'!");
                }
            }

            return location;
        }
    }
}