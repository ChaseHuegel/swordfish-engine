namespace WaywardBeyond.Data.Levels;

/// <summary>Composite primary key identifying one persisted block-entity in <c>level.db</c>.</summary>
public readonly record struct BlockEntityKey(ulong VoxelEntityUuid, int VoxelX, int VoxelY, int VoxelZ);
