namespace WaywardBeyond.Data.Levels;

/// <summary>Composite primary key identifying one persisted brick-entity in <c>level.db</c>.</summary>
public readonly record struct BrickEntityKey(ulong VoxelEntityUuid, int VoxelX, int VoxelY, int VoxelZ);
