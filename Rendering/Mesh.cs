using System.IO;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Swordfish.Diagnostics;
using System.Linq;

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
        public string name = "";
        public bool doublesided = true;

        public Vector3 origin = Vector3.Zero;

        public uint[] triangles;
        public Vector3[] vertices;
        public Vector4[] colors;
        public Vector3[] normals;
        public Vector3[] uv;

        private int VAO, VBO, EBO;

        private Shader shader;
        public Shader GetShader() => shader;

        private Texture texture;
        public Texture GetTexture() => texture;

        public MeshData GetRawData()
        {
            float[] raw = new float[ vertices.Length * 13 ];

            int row;
            for (int i = 0; i < vertices.Length; i++)
            {
                row = i * 13;
                raw[row] = vertices[i].X + origin.X;
                raw[row+1] = vertices[i].Y + origin.Y;
                raw[row+2] = vertices[i].Z + origin.Z;

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

        public static Mesh LoadFromFile(string path, string name)
        {
            List<uint> t = new List<uint>();
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<Vector3> u = new List<Vector3>();

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
                    switch (token)
                    {
                        //  Vertices
                        case "v":
                            v.Add(new Vector3( float.Parse(entries[1]), float.Parse(entries[2]), float.Parse(entries[3]) ));
                        break;

                        //  UV, account for 2d and 3d UV coords
                        case "vt":
                            u.Add(new Vector3( float.Parse(entries[1]), float.Parse(entries[2]), 1f ));
                        break;

                        //  Normals
                        case "vn":
                            n.Add(new Vector3( float.Parse(entries[1]), float.Parse(entries[2]), float.Parse(entries[3]) ));
                        break;

                        //  Triangles
                        case "f":
                            string[] indicies;

                            //  Forcibly use 3 indicies, quads not supported
                            indicies = entries[1].Split('/');
                            t.Add(uint.Parse(indicies[0]) - 1);

                            indicies = entries[2].Split('/');
                            t.Add(uint.Parse(indicies[0]) - 1);

                            indicies = entries[3].Split('/');
                            t.Add(uint.Parse(indicies[0]) - 1);
                        break;

                        default: break;
                    }
                }
            }

            //  Fill any missing data
            if (n.Count < v.Count) n.AddRange(new Vector3[v.Count - n.Count]);
            if (u.Count < v.Count) u.AddRange(new Vector3[v.Count - u.Count]);

            Debug.Log($"    Model '{name}' loaded");

            //  Build the mesh
            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.triangles = t.ToArray();
            mesh.vertices = v.ToArray();
            mesh.normals = n.ToArray();
            mesh.uv = u.ToArray();

            //  OBJ doesn't support colors, initialize defaults
            mesh.colors = new Vector4[v.Count];

            return mesh;
        }

        public void Bind(Shader shader, Texture texture)
        {
            MeshData data = GetRawData();

            //  Setup vertex buffer
            VBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, data.vertices.Length * sizeof(float), data.vertices, BufferUsageHint.StaticDraw);

            //  Setup VAO and tell openGL how to interpret vertex data
            VAO = GL.GenVertexArray();
            GL.BindVertexArray(VAO);

            int attrib = shader.GetAttribLocation("in_position");
            GL.VertexAttribPointer(attrib, 3, VertexAttribPointerType.Float, false, 13 * sizeof(float), 0);
            GL.EnableVertexAttribArray(attrib);

            attrib = shader.GetAttribLocation("in_color");
            GL.VertexAttribPointer(attrib, 4, VertexAttribPointerType.Float, false, 13 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(attrib);

            attrib = shader.GetAttribLocation("in_uv");
            GL.VertexAttribPointer(attrib, 3, VertexAttribPointerType.Float, false, 13 * sizeof(float), 10 * sizeof(float));
            GL.EnableVertexAttribArray(attrib);

            //  Setup element buffer
            EBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, data.triangles.Length * sizeof(uint), data.triangles, BufferUsageHint.StaticDraw);

            this.shader = shader;
            this.texture = texture;
        }

        public void Render()
        {
            if (doublesided)
                GL.Disable(EnableCap.CullFace);
            else
                GL.Enable(EnableCap.CullFace);

            shader.Use();
            texture.Use(TextureUnit.Texture0);

            GL.BindVertexArray(VAO);
            GL.DrawElements(PrimitiveType.Triangles, triangles.Length, DrawElementsType.UnsignedInt, 0);
        }
    }
}