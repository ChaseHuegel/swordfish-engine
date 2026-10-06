using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Server.Core.Saves;

/// <summary>
/// Server-owned level storage: creates, lists, deletes, and opens per-level save databases.
/// </summary>
public interface ILevelCatalog
{
    /// <summary>
    /// Generates a new level from a name and seed and persists its metadata plus one entity per
    /// structure. Runs the shared deterministic generator; expect to call it off the server tick thread.
    /// </summary>
    bool Create(string name, string seed, GameMode gameMode, out string levelGuid);

    /// <summary>Enumerates every saved level's metadata for the save-listing UI.</summary>
    Level[] ListLevels();

    /// <summary>Deletes a saved level, including its entities and character locations.</summary>
    bool Delete(string levelGuid);

    bool Exists(string levelGuid);

    /// <summary>Opens the level's store, or returns null when no such level exists on disk.</summary>
    ILevelStore? Open(string levelGuid);
}
