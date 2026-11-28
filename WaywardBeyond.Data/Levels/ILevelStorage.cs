using System;
using System.Collections.Generic;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Levels;

/// <summary>Provides access to one level's data.</summary>
public interface ILevelStorage : IDisposable
{
    /// <summary>Attempts to get the level information.</summary>
    Result<Level> GetLevel();

    /// <summary>Gets the level's voxel entities.</summary>
    IReadOnlyList<VoxelEntityData> GetVoxelEntities(uint dataVersion);

    /// <summary>Gets the level's persisted block-entities.</summary>
    IReadOnlyList<BlockEntityData> GetBlockEntities() => [];

    /// <summary>Attempts to get a character's entity.</summary>
    Result<CharacterEntityData> GetCharacterEntity(ulong uuid);

    /// <summary>Attempts to commit a full snapshot in one transaction.</summary>
    Result Save(Level level, IReadOnlyList<VoxelEntityData> voxelEntities, IReadOnlyList<CharacterEntityData> characterEntities);

    /// <summary>Attempts to commit a full snapshot including block-entities in one transaction.</summary>
    Result Save(
        Level level,
        IReadOnlyList<VoxelEntityData> voxelEntities,
        IReadOnlyList<CharacterEntityData> characterEntities,
        IReadOnlyList<BlockEntityData> blockEntities
    ) => Save(level, voxelEntities, characterEntities);

    /// <summary>Deletes orphaned block-entity records keyed by <paramref name="keys"/> in one transaction.</summary>
    Result DeleteBlockEntities(IReadOnlyList<BlockEntityKey> keys) => Result.FromSuccess();

    /// <summary>Attempts to save the level's information.</summary>
    Result Save(Level level);

    /// <summary>Attempts to save a character's entity.</summary>
    Result Save(CharacterEntityData characterEntity);
}