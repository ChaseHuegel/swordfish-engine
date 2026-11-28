using Krill;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// ECS-thread system that stages UI-queued <see cref="StationActionOp"/> commands onto the local player
/// and mirrors the server-replicated <see cref="ActiveStationComponent"/> into <see cref="ClientStationState"/>.
/// </summary>
internal sealed class ClientStationSystem(
    in ClientStationState stationState,
    in PlayerData playerData
) : IEntitySystem
{
    private readonly ClientStationState _stationState = stationState;
    private readonly PlayerData _playerData = playerData;

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        if (WaywardBeyond.GameState < GameState.Playing)
        {
            _stationState.Reset();
            return;
        }

        while (_stationState.TryDequeueOutbound(out StationActionOp op))
        {
            _playerData.StageStationAction(store, in op);
        }

        ActiveStationComponent active = _playerData.GetActiveStation(store);
        _stationState.UpdateReplicatedState(in active);
    }
}
