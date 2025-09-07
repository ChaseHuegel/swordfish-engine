using Swordfish.Bricks;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Bricks;

internal sealed class BrickInfo(string id, ushort dataID, bool transparent, Mesh? mesh, BrickShape shape, BrickTextures textures)
{
    public string ID = id;
    public ushort DataID = dataID;
    public bool Transparent = transparent;
    public Mesh? Mesh = mesh;
    public BrickShape Shape = shape;
    public BrickTextures Textures = textures;
    
    public Brick GetBrick()
    {
        return new Brick(DataID);
    }
}