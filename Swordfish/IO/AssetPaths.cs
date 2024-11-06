using Swordfish.Library.IO;

namespace Swordfish.IO;

public static class AssetPaths
{
    public static PathInfo Root { get; } = new("");
    public static PathInfo Shaders { get; } = new("shaders/");
    public static PathInfo Textures { get; } = new("textures/");
    public static PathInfo Fonts { get; } = new("fonts/");
    public static PathInfo Models { get; } = new("models/");
}