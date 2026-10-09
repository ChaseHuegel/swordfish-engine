using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Saves;

internal readonly struct GameSave(in string name, in Level level)
{
    public readonly string Name = name;
    public readonly Level Level = level;
}
