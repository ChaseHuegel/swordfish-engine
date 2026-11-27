using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Saves;

/// <summary>Parameters for creating a new galaxy save.</summary>
internal struct GameOptions(in string name, in string seed, in GameMode defaultGameMode = GameMode.Creative)
{
    public string Name = name;
    public string Seed = seed;
    public GameMode DefaultGameMode = defaultGameMode;
}