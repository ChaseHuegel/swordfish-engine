using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Saves;

internal readonly struct GameSave(in string name, in Level level)
{
    public readonly string Name = name;
    public readonly Level Level = level;
}
