using System.Collections.Generic;
using System.IO;
using System.Linq;

using OpenTK.Mathematics;

using Swordfish.Library.Diagnostics;
using Swordfish.Library.Types;

namespace Swordfish.Engine.Rendering
{
    public static class OBJ
    {
        /// <summary>
        /// Export a mesh to an OBJ model named "{mesh.Name}.obj"
        /// </summary>
        /// <param name="path">location to safe the file</param>
        /// <param name="useOrigin">use the mesh's origin in the exported OBJ</param>
        /// <returns>path to the exported OBJ</returns>
        public static string ExportToOBJ(Mesh mesh, string path, bool useOrigin = true)
        {
            if (!Directory.Exists(path))
            {
                Debug.Log($"Directory'{path}' not found, creating it...");
                Directory.CreateDirectory(path);
            }

            using (StreamWriter stream = File.CreateText($"{path}{mesh.Name}.obj"))
            {
                stream.WriteLine("# Swordfish Engine exported OBJ");
                stream.WriteLine("# https://github.com/ChaseHuegel/swordfish-engine ");
                stream.WriteLine($"o {mesh.Name}");

                foreach (Vector3 vec in mesh.vertices)
                    if (useOrigin)
                        stream.WriteLine($"v {vec.X + mesh.Origin.X} {vec.Y + mesh.Origin.Y} {vec.Z + mesh.Origin.Z}");
                    else
                        stream.WriteLine($"v {vec.X} {vec.Y} {vec.Z}");

                foreach (Vector3 vec in mesh.uv)
                    stream.WriteLine($"vt {vec.X} {vec.Y} {vec.Z}");

                foreach (Vector3 vec in mesh.normals)
                    stream.WriteLine($"vn {vec.X} {vec.Y} {vec.Z}");

                int triangle = 1;
                foreach (uint index in mesh.triangles)
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
        public static Mesh LoadFromFile(string path, string name, float scale = 1f)
        {
            List<uint> vertexIndicies = new List<uint>();
            List<uint> uvIndicies = new List<uint>();
            List<uint> normalIndicies = new List<uint>();

            List<Vector3> vertexData = new List<Vector3>();
            List<Vector3> normalData = new List<Vector3>();
            List<Vector3> uvData = new List<Vector3>();

            List<uint> triangles = new List<uint>();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector3> uv = new List<Vector3>();
            List<Vector4> colors = new List<Vector4>();

            uint triangleIndex = 0;

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
                            vertexData.Add(new Vector3(float.Parse(entries[0]), float.Parse(entries[1]), float.Parse(entries[2])));
                            break;

                        //  UV, account for 2d and 3d UV coords
                        case "vt":
                            uvData.Add(new Vector3(float.Parse(entries[0]), float.Parse(entries[1]), entries.Count < 3 ? 0f : float.Parse(entries[2])));
                            break;

                        //  Normals
                        case "vn":
                            normalData.Add(new Vector3(float.Parse(entries[0]), float.Parse(entries[1]), float.Parse(entries[2])));
                            break;

                        //  Triangles
                        case "f":
                            List<string> values = new List<string>();
                            values.AddRange(entries[0].Split('/'));
                            values.AddRange(entries[1].Split('/'));
                            values.AddRange(entries[2].Split('/'));

                            vertexIndicies.Add(uint.Parse(values[0]) - 1);
                            uvIndicies.Add(uint.Parse(values[1]) - 1);
                            normalIndicies.Add(uint.Parse(values[2]) - 1);

                            vertexIndicies.Add(uint.Parse(values[3]) - 1);
                            uvIndicies.Add(uint.Parse(values[4]) - 1);
                            normalIndicies.Add(uint.Parse(values[5]) - 1);

                            vertexIndicies.Add(uint.Parse(values[6]) - 1);
                            uvIndicies.Add(uint.Parse(values[7]) - 1);
                            normalIndicies.Add(uint.Parse(values[8]) - 1);

                            triangles.Add(triangleIndex);
                            triangles.Add(triangleIndex + 1);
                            triangles.Add(triangleIndex + 2);
                            triangleIndex += 3;
                            break;

                        default: break;
                    }
                }
            }

            //  Process data...

            foreach (uint vertexIndex in vertexIndicies)
            {
                Vector3 vertex = vertexData[(int)vertexIndex];
                vertices.Add(vertex * scale);

                colors.Add(Color.White);
            }

            foreach (uint uvIndex in uvIndicies)
            {
                Vector3 vt = uvData[(int)uvIndex];
                uv.Add(vt);
            }

            foreach (uint normalIndex in normalIndicies)
            {
                Vector3 normal = normalData[(int)normalIndex];
                normals.Add(normal);
            }

            //  Build the mesh...
            Mesh mesh = new Mesh();
            mesh.Name = name;
            mesh.triangles = triangles.ToArray();
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uv.ToArray();
            mesh.colors = colors.ToArray();

            return mesh;
        }
    }
}
