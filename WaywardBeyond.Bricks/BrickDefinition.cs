namespace WaywardBeyond.Bricks;

/// <summary>Defines a brick asset.</summary>
internal struct BrickDefinition()
{
    public string ID;
    public bool Transparent;
    public bool Passable;
    public string? Mesh;
    public BrickShape Shape;
    public BrickTextures Textures;
    public string[] Tags;
}