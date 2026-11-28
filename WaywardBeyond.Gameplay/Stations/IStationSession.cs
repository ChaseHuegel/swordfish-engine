using Krill;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Gameplay.Stations;

/// <summary>Per-world station lifecycle and authorization surface exposed to interaction and station action handlers.</summary>
public interface IStationSession
{
    /// <summary>Opens the brick-entity anchored at (<paramref name="voxelEntityUuid"/>, <paramref name="voxelCoord"/>) for <paramref name="playerEntity"/>.</summary>
    Result OpenBrickStation(DataStore store, int playerEntity, Uuid clientId, Uuid voxelEntityUuid, Int3 voxelCoord);

    /// <summary>Opens <paramref name="stationEntity"/> for <paramref name="playerEntity"/> with explicit metadata and viewer subscription.</summary>
    Result OpenStation(
        DataStore store,
        int playerEntity,
        Uuid clientId,
        int stationEntity,
        Uuid voxelEntityUuid,
        Int3 voxelCoord,
        string stationTag,
        StationUiKind uiKind
    );

    /// <summary>Closes any active station on <paramref name="playerEntity"/> and detaches its viewer subscription.</summary>
    bool CloseStation(DataStore store, int playerEntity, Uuid clientId);

    /// <summary>Validates that <paramref name="playerEntity"/> is alive and authorized to operate brick-entity <paramref name="targetUuid"/> within reach.</summary>
    Result<int> AuthorizeBrickEntity(DataStore store, int playerEntity, Uuid clientId, Uuid targetUuid);

    /// <summary>Returns whether <paramref name="clientId"/> holds <paramref name="permission"/>.</summary>
    bool HasPermission(Uuid clientId, string permission);

    /// <summary>Returns whether <paramref name="playerEntity"/> is currently dead.</summary>
    bool IsPlayerDead(DataStore store, int playerEntity);
}
