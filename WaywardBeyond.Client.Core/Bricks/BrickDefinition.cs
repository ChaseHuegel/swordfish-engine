using Swordfish.Bricks;

namespace WaywardBeyond.Client.Core.Bricks;

public struct BrickDefinition()
{
    public string ID;
    public string? Mesh;
    public BrickShape Shape;
    public BrickTextures Textures;

    public Brick GetBrick()
    {
        return new Brick((ushort)(Shape + 1))
        {
            Name = Textures.Default,
        };
    }
}