namespace Swordfish.Graphics.SilkNET.OpenGL;

internal static class MeshExtensions
{
    public static float[] GetRawVertexData(this Mesh mesh)
    {
        float[] vertexData = new float[mesh.Vertices.Length * 10];

        for (int i = 0; i < mesh.Vertices.Length; i++)
        {
            vertexData[i + 0] = mesh.Vertices[i].X;
            vertexData[i + 1] = mesh.Vertices[i].Y;
            vertexData[i + 2] = mesh.Vertices[i].Z;

            vertexData[i + 3] = mesh.Colors[i].X;
            vertexData[i + 4] = mesh.Colors[i].Y;
            vertexData[i + 5] = mesh.Colors[i].Z;
            vertexData[i + 6] = mesh.Colors[i].W;

            vertexData[i + 7] = mesh.UV[i].X;
            vertexData[i + 8] = mesh.UV[i].Y;
            vertexData[i + 9] = mesh.UV[i].Z;
        }

        return vertexData;
    }
}