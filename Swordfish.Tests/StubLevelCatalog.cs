using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Shared.Data;

namespace Swordfish.Tests;

/// <summary>
/// Headless level catalog for tests that never touch a level save. Opening any level yields no store,
/// so joins fall back to the default spawn instead of touching disk.
/// </summary>
internal sealed class StubLevelCatalog : ILevelCatalog
{
    public bool Create(string name, string seed, GameMode gameMode, out string levelGuid)
    {
        levelGuid = string.Empty;
        return false;
    }

    public Level[] ListLevels() => [];

    public bool Delete(string levelGuid) => true;

    public bool Exists(string levelGuid) => false;

    public ILevelStore? Open(string levelGuid) => null;
}
