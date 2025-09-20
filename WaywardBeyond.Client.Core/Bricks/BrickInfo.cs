using Swordfish.Bricks;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Bricks;

internal sealed class BrickInfo(string id, ushort dataID, bool transparent, bool passable, Mesh? mesh, BrickShape shape, BrickTextures textures, string[]? tags)
{
    public string ID = id;
    public ushort DataID = dataID;
    public bool Transparent = transparent;
    public bool Passable = passable;
    public Mesh? Mesh = mesh;
    public BrickShape Shape = shape;
    public BrickTextures Textures = textures;
    public string[] Tags = tags ?? [];

    private readonly byte _shapeData = shape == BrickShape.Any ? (byte)BrickShape.Block : (byte)shape;
    
    public Brick GetBrick()
    {
        return new Brick(DataID, _shapeData);
    }
}