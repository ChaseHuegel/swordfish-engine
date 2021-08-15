using System.IO;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Swordfish.Diagnostics;
using System.Linq;
using Swordfish.Types;

namespace Swordfish.Rendering
{
    public class MeshData
    {
        public uint[] triangles;
        public float[] vertices;

        public MeshData(uint[] triangles, float[] vertices)
        {
            this.triangles = triangles;
            this.vertices = vertices;
        }
    }

    public class Mesh
    {
        public string Name = "";
        public bool DoubleSided = true;
        public Vector3 Origin = Vector3.Zero;

        public Shader Shader;
        public Texture Texture;

        public uint[] triangles;
        public Vector3[] vertices;
        public Vector4[] colors;
        public Vector3[] normals;
        public Vector3[] uv;

        internal int VAO, VBO, EBO;

        /// <summary>
        /// Translates the mesh into data for using directly with openGL
        /// </summary>
        /// <returns></returns>
        public MeshData GetRawData()
        {
            float[] raw = new float[ vertices.Length * 13 ];

            int row;
            for (int i = 0; i < vertices.Length; i++)
            {
                row = i * 13;
                raw[row] = vertices[i].X + Origin.X;
                raw[row+1] = vertices[i].Y + Origin.Y;
                raw[row+2] = vertices[i].Z + Origin.Z;

                raw[row+3] = colors[i].X;
                raw[row+4] = colors[i].Y;
                raw[row+5] = colors[i].Z;
                raw[row+6] = colors[i].W;

                raw[row+7] = normals[i].X;
                raw[row+8] = normals[i].Y;
                raw[row+9] = normals[i].Z;

                raw[row+10] = uv[i].X;
                raw[row+11] = uv[i].Y;
                raw[row+12] = uv[i].Z;
            }

            return new MeshData(triangles, raw);
        }

        /// <summary>
        /// Bind this mesh to openGL data buffers
        /// </summary>
        internal void Bind()
        {
            MeshData data = GetRawData();

            //  Setup vertex buffer
            VBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, data.vertices.Length * sizeof(float), data.vertices, BufferUsageHint.StaticDraw);

            //  Setup VAO and tell openGL how to interpret vertex data
            VAO = GL.GenVertexArray();
            GL.BindVertexArray(VAO);

            int attrib = Shader.GetAttribLocation("in_position");
            GL.VertexAttribPointer(attrib, 3, VertexAttribPointerType.Float, false, 13 * sizeof(float), 0);
            GL.EnableVertexAttribArray(attrib);

            attrib = Shader.GetAttribLocation("in_color");
            GL.VertexAttribPointer(attrib, 4, VertexAttribPointerType.Float, false, 13 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(attrib);

            attrib = Shader.GetAttribLocation("in_uv");
            GL.VertexAttribPointer(attrib, 3, VertexAttribPointerType.Float, false, 13 * sizeof(float), 10 * sizeof(float));
            GL.EnableVertexAttribArray(attrib);

            //  Setup element buffer
            EBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, data.triangles.Length * sizeof(uint), data.triangles, BufferUsageHint.StaticDraw);
        }

        /// <summary>
        /// Make a draw call to render this mesh using openGL
        /// <para/> This must be used within a rendering context, this is not immediate call
        /// </summary>
        internal void Render()
        {
            if (DoubleSided)
                GL.Disable(EnableCap.CullFace);
            else
                GL.Enable(EnableCap.CullFace);

            Shader.Use();
            Texture.Use(TextureUnit.Texture0);

            GL.BindVertexArray(VAO);
            GL.DrawElements(PrimitiveType.Triangles, triangles.Length, DrawElementsType.UnsignedInt, 0);
        }

        /// <summary>
        /// Export this mesh to an OBJ model named "{mesh.Name}.obj"
        /// </summary>
        /// <param name="path">location to safe the file</param>
        /// <param name="useOrigin">use the mesh's origin in the exported OBJ</param>
        /// <returns>path to the exported OBJ</returns>
        public string ExportToOBJ(string path, bool useOrigin = true)
        {
            if (!Directory.Exists(path))
            {
                Debug.Log($"Directory'{path}' not found, creating it...");
                Directory.CreateDirectory(path);
            }

            using (StreamWriter stream = File.CreateText($"{path}{Name}.obj"))
            {
                stream.WriteLine("# Swordfish Engine exported OBJ");
                stream.WriteLine("# https://github.com/ChaseHuegel/swordfish-engine ");
                stream.WriteLine($"o {Name}");

                foreach (Vector3 vec in vertices)
                    if (useOrigin)
                        stream.WriteLine($"v {vec.X+Origin.X} {vec.Y+Origin.Y} {vec.Z+Origin.Z}");
                    else
                        stream.WriteLine($"v {vec.X} {vec.Y} {vec.Z}");

                foreach (Vector3 vec in uv)
                    stream.WriteLine($"vt {vec.X} {vec.Y} {vec.Z}");

                foreach (Vector3 vec in normals)
                    stream.WriteLine($"vn {vec.X} {vec.Y} {vec.Z}");

                int triangle = 1;
                foreach (uint index in triangles)
                {
                    stream.WriteLine($"f {index + 1}/{triangle}/{triangle}");
                    triangle++;
                }
            }

            return path;
        }

        /// <summary>
        /// Creates a mesh from an OBJ model
        /// </summary>
        /// <param name="path">location of the obj file</param>
        /// <param name="name">name of the created mesh</param>
        /// <returns>instance of Mesh created from the OBJ</returns>
        public static Mesh LoadFromFile(string path, string name)
        {
            List<uint> t = new List<uint>();
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<Vector3> u = new List<Vector3>();
            List<Vector4> c = new List<Vector4>();

            if (!File.Exists(path))
			{
				Debug.Log($"Unable to load model '{name}' from '{path}', file not found", LogType.ERROR);
                return null;
            }

            Debug.Log($"Loading model '{name}' from '{path}'");

            using (StreamReader reader = new StreamReader(path))
            {
                while (!reader.EndOfStream)
                {
                    //  Split the current line
                    List<string> entries = reader.ReadLine().ToLower().Split(' ').ToList<string>();

                    //  Remove any white spaces
                    entries.RemoveAll(x => x == string.Empty);

                    //  Continue if this line is empty
                    if (entries.Count <= 0) continue;

                    string token = entries[0];
                    entries.RemoveAt(0);

                    switch (token)
                    {
                        //  Vertices
                        case "v":
                            v.Add(new Vector3( float.Parse(entries[0]), float.Parse(entries[1]), float.Parse(entries[2]) ));
                        break;

                        //  UV, account for 2d and 3d UV coords
                        case "vt":
                            u.Add(new Vector3( float.Parse(entries[0]), float.Parse(entries[1]), entries.Count < 3 ? 0f : float.Parse(entries[2]) ));
                        break;

                        //  Normals
                        case "vn":
                            n.Add(new Vector3( float.Parse(entries[0]), float.Parse(entries[1]), float.Parse(entries[2]) ));
                        break;

                        //  Triangles
                        case "f":
                            foreach (string entry in entries)
                            {
                                string[] indicies = entry.Split('/');
                                t.Add(uint.Parse(indicies[0]) - 1);
                            }
                        break;

                        default: break;
                    }
                }
            }

            //  OBJ doesn't save color info, default to white
            for (int i = 0; i < v.Count; i++)
                c.Add(Color.White);

            if (n.Count < v.Count)
            {
                Debug.Log("    Missing some normals data, filling defaults");
                n.AddRange(new Vector3[v.Count - n.Count]);
            }

            if (u.Count < v.Count)
            {
                Debug.Log("    Missing some UV data, filling defaults");
                u.AddRange(new Vector3[v.Count - u.Count]);
            }

            Debug.Log($"    Model '{name}' loaded");

            //  Build the mesh
            Mesh mesh = new Mesh();
            mesh.Name = name;
            mesh.triangles = t.ToArray();
            mesh.vertices = v.ToArray();
            mesh.normals = n.ToArray();
            mesh.uv = u.ToArray();
            mesh.colors = c.ToArray();

            return mesh;
        }
    }
}