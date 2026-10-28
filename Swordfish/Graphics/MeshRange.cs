namespace Swordfish.Graphics;

internal struct MeshRange
{
    public bool Active;
    public int Generation;
    public int Arena;
    public int VertexOffset;
    public int IndexOffset;
    public int VertexCapacity;
    public int IndexCapacity;
    public int VertexCount;
    public int IndexCount;
    public BoundingBox Bounds;
    public float[] VertexData;
    public uint[] Indices;
    public uint Version;
}