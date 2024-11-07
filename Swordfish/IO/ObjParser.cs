using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Library.IO;

namespace Swordfish.IO;

internal class ObjParser : IFileParser<Mesh>
{
    public string[] SupportedExtensions { get; } =
    [
        ".obj",
    ];

    object IFileParser.Parse(PathInfo file) => Parse(file);
    public Mesh Parse(PathInfo file)
    {
        using Stream stream = file.Open();
        using StreamReader reader = new(stream);
        return ParseFromReader(reader);
    }

    private static Mesh ParseFromReader(StreamReader reader, float scale = 1f)
    {
        List<uint> vertexIndices = [];
        List<uint> uvIndices = [];
        List<uint> normalIndices = [];

        List<Vector3> vertexData = [];
        List<Vector3> normalData = [];
        List<Vector3> uvData = [];

        List<uint> triangles = [];
        List<Vector3> vertices = [];
        List<Vector3> normals = [];
        List<Vector3> uv = [];
        List<Vector4> colors = [];

        uint triangleIndex = 0;

        while (!reader.EndOfStream)
        {
            List<string> entries = reader.ReadLine()?.ToLower().Split(' ').ToList() ?? [];

            //  Remove any whitespace
            entries.RemoveAll(string.IsNullOrWhiteSpace);

            if (entries.Count <= 0)
            {
                continue;
            }

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
                    List<string> values = [];
                    values.AddRange(entries[0].Split('/'));
                    values.AddRange(entries[1].Split('/'));
                    values.AddRange(entries[2].Split('/'));

                    vertexIndices.Add(uint.Parse(values[0]) - 1);
                    uvIndices.Add(uint.Parse(values[1]) - 1);
                    normalIndices.Add(uint.Parse(values[2]) - 1);

                    vertexIndices.Add(uint.Parse(values[3]) - 1);
                    uvIndices.Add(uint.Parse(values[4]) - 1);
                    normalIndices.Add(uint.Parse(values[5]) - 1);

                    vertexIndices.Add(uint.Parse(values[6]) - 1);
                    uvIndices.Add(uint.Parse(values[7]) - 1);
                    normalIndices.Add(uint.Parse(values[8]) - 1);

                    triangles.Add(triangleIndex);
                    triangles.Add(triangleIndex + 1);
                    triangles.Add(triangleIndex + 2);
                    triangleIndex += 3;
                    break;
            }
        }

        foreach (uint vertexIndex in vertexIndices)
        {
            Vector3 vertex = vertexData[(int)vertexIndex];
            vertices.Add(vertex * scale);
            colors.Add(new Vector4(1f));
        }

        foreach (uint uvIndex in uvIndices)
        {
            Vector3 vt = uvData[(int)uvIndex];
            uv.Add(vt);
        }

        foreach (uint normalIndex in normalIndices)
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
