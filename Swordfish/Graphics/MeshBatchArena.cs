namespace Swordfish.Graphics;

internal sealed class MeshBatchArena
{
    public int VertexCapacity;
    public int IndexCapacity;
    public int UsedVertices;
    public int UsedIndices;
    public int WasteVertices;
    public int WasteIndices;
}