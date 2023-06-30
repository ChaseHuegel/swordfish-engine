using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Library.IO;

namespace Swordfish.IO;

internal class OBJParser : IFileParser<Mesh>
{
    public string[] SupportedExtensions { get; } = new string[] {
        ".obj"
    };

    object IFileParser.Parse(IFileService fileService, IPath file) => Parse(fileService, file);
    public Mesh Parse(IFileService fileService, IPath file)
    {
        using Stream stream = fileService.Open(file);
        using StreamReader reader = new(stream);
        return ParseFromReader(reader);
    }

    private static Mesh ParseFromReader(StreamReader reader, float scale = 1f)
    {
        List<uint> vertexIndicies = new();
        List<uint> uvIndicies = new();
        List<uint> normalIndicies = new();

        List<Vector3> vertexData = new();
        List<Vector3> normalData = new();
        List<Vector3> uvData = new();

        List<uint> triangles = new();
        List<Vector3> vertices = new();
        List<Vector3> normals = new();
        List<Vector3> uv = new();
        List<Vector4> colors = new();

        uint triangleIndex = 0;

        while (!reader.EndOfStream)
        {
            List<string> entries = reader.ReadLine()?.ToLower().Split(' ').ToList() ?? new List<string>();

            //  Remove any whitespace
            entries.RemoveAll(string.IsNullOrWhiteSpace);

            if (entries.Count <= 0)
                continue;

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
                    List<string> values = new();
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
            }
        }

        foreach (uint vertexIndex in vertexIndicies)
        {
            Vector3 vertex = vertexData[(int)vertexIndex];
            vertices.Add(vertex * scale);
            colors.Add(new Vector4(1f));
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

        return new Mesh(
            triangles.ToArray(),
            vertices.ToArray(),
            colors.ToArray(),
            uv.ToArray(),
            normals.ToArray()
        );
    }
}
