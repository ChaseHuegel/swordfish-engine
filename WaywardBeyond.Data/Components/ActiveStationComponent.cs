using System;
using Krill;

namespace WaywardBeyond.Data.Components;

/// <summary>
/// Server-authoritative <c>OwnerOnly</c> binding identifying the station or terminal a player currently has open.
/// </summary>
public partial struct ActiveStationComponent : IDataComponent
{
    /// <summary>Cleared state indicating no station is open.</summary>
    public static readonly ActiveStationComponent None = new();

    /// <summary>Creates a cleared active station component with no bound station.</summary>
    public ActiveStationComponent()
        : this(
            _StationEntity: 0UL,
            _VoxelEntity: 0UL,
            _VoxelX: 0,
            _VoxelY: 0,
            _VoxelZ: 0,
            _StationTag: string.Empty,
            _UiKind: (byte)StationUiKind.None)
    {
    }

    /// <summary>Creates an active station component bound to a brick-entity and voxel coordinate.</summary>
    public ActiveStationComponent(
        ulong stationEntity,
        ulong voxelEntity,
        int voxelX,
        int voxelY,
        int voxelZ,
        string? stationTag,
        StationUiKind uiKind
    )
        : this(
            _StationEntity: stationEntity,
            _VoxelEntity: voxelEntity,
            _VoxelX: voxelX,
            _VoxelY: voxelY,
            _VoxelZ: voxelZ,
            _StationTag: stationTag ?? string.Empty,
            _UiKind: (byte)uiKind)
    {
    }

    /// <summary>Whether a station entity is currently bound.</summary>
    public readonly bool IsOpen => StationEntity != 0UL;

    /// <summary>The typed client UI kind for this station.</summary>
    public readonly StationUiKind Kind => (StationUiKind)UiKind;

    /// <summary>Returns whether every replicated field matches <paramref name="other"/>.</summary>
    public readonly bool EqualsState(in ActiveStationComponent other)
    {
        return StationEntity == other.StationEntity
            && VoxelEntity == other.VoxelEntity
            && VoxelX == other.VoxelX
            && VoxelY == other.VoxelY
            && VoxelZ == other.VoxelZ
            && string.Equals(StationTag ?? string.Empty, other.StationTag ?? string.Empty, StringComparison.Ordinal)
            && UiKind == other.UiKind;
    }
}
