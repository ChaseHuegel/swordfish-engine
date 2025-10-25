using Swordfish.Library.IO;

namespace WaywardBeyond.Client.Core.Saves;

internal readonly struct GameSave(in PathInfo path, in string name, in Level level)
{
    public readonly PathInfo Path = path;
    public readonly string Name = name;
    public readonly Level Level = level;
}