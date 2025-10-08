using Swordfish.Library.IO;

namespace WaywardBeyond.Client.Core.Saves;

internal readonly struct GameSave(in PathInfo path, in string name)
{
    public readonly PathInfo Path = path;
    public readonly string Name = name;
}