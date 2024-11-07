namespace Swordfish.Graphics.SilkNET.OpenGL;

internal static class MeshExtensions
{
    private const int MAX_VERTICES = int.MaxValue / GLRenderTarget.VERTEX_DATA_LENGTH;

    public static float[] GetRawVertexData(this Mesh mesh)
    {
        int vertexCount = mesh.Vertices.Length;
        if (vertexCount > MAX_VERTICES)
        {
            throw new InvalidOperationException($"Meshes cannot exceed {MAX_VERTICES} vertices.");
        }

        //  Throw on int overflows to be safe
        int dataLength = checked(vertexCount * GLRenderTarget.VERTEX_DATA_LENGTH);
        var vertexData = new float[dataLength];

        for (var i = 0; i < vertexCount; i++)
        {
            int dataOffset = i * GLRenderTarget.VERTEX_DATA_LENGTH;

            vertexData[dataOffset + 0] = mesh.Vertices[i].X;
            vertexData[dataOffset + 1] = mesh.Vertices[i].Y;
            vertexData[dataOffset + 2] = mesh.Vertices[i].Z;

            vertexData[dataOffset + 3] = mesh.Colors[i].X;
            vertexData[dataOffset + 4] = mesh.Colors[i].Y;
            vertexData[dataOffset + 5] = mesh.Colors[i].Z;
            vertexData[dataOffset + 6] = mesh.Colors[i].W;

            vertexData[dataOffset + 7] = mesh.Uv[i].X;
            vertexData[dataOffset + 8] = mesh.Uv[i].Y;
            vertexData[dataOffset + 9] = mesh.Uv[i].Z;

            vertexData[dataOffset + 10] = mesh.Normals[i].X;
            vertexData[dataOffset + 11] = mesh.Normals[i].Y;
            vertexData[dataOffset + 12] = mesh.Normals[i].Z;
        }

        return vertexData;
    }
}