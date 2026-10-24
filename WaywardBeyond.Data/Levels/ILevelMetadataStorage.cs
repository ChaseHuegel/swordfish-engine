using System.Collections.Generic;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Levels;

/// <summary>Provides access to level metadata.</summary>
public interface ILevelMetadataStorage
{
    /// <summary>Attempts to get metadata for the provided level.</summary>
    Result<LevelMetadata> Get(string levelGuid);

    /// <summary>Gets metadata for all levels.</summary>
    IReadOnlyDictionary<string, LevelMetadata> GetAll();

    /// <summary>Attempts to save metadata for the provided level.</summary>
    Result Save(string levelGuid, LevelMetadata meta);

    /// <summary>Attempts to delete metadata for the provided level.</summary>
    Result Delete(string levelGuid);
}