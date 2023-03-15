namespace Swordfish.Graphics.SilkNET.OpenGL;

internal static class MeshExtensions
{
    public static float[] GetRawVertexData(this Mesh mesh)
    {
        float[] vertexData = new float[mesh.Vertices.Length * GLRenderTarget.VertexDataLength];

        for (int i = 0; i < mesh.Vertices.Length; i++)
        {
            int dataOffset = i * GLRenderTarget.VertexDataLength;

            vertexData[dataOffset + 0] = mesh.Vertices[i].X;
            vertexData[dataOffset + 1] = mesh.Vertices[i].Y;
            vertexData[dataOffset + 2] = mesh.Vertices[i].Z;

            vertexData[dataOffset + 3] = mesh.Colors[i].X;
            vertexData[dataOffset + 4] = mesh.Colors[i].Y;
            vertexData[dataOffset + 5] = mesh.Colors[i].Z;
            vertexData[dataOffset + 6] = mesh.Colors[i].W;

            vertexData[dataOffset + 7] = mesh.UV[i].X;
            vertexData[dataOffset + 8] = mesh.UV[i].Y;
            vertexData[dataOffset + 9] = mesh.UV[i].Z;

            vertexData[dataOffset + 10] = mesh.Normals[i].X;
            vertexData[dataOffset + 11] = mesh.Normals[i].Y;
            vertexData[dataOffset + 12] = mesh.Normals[i].Z;
        }

        return vertexData;
    }
}