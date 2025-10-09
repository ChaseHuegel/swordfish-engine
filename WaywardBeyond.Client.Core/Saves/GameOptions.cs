namespace WaywardBeyond.Client.Core.Saves;

internal struct GameOptions(in string name, in string seed)
{
    public string Name = name;
    public string Seed = seed;
}