namespace WaywardBeyond.Client.Core.Bricks;

public struct BrickDefinition()
{
    public string ID;
    public bool Transparent;
    public string? Mesh;
    public BrickShape Shape;
    public BrickTextures Textures;
}