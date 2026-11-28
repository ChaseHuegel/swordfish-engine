using Krill;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Stations;

/// <summary>Server-authoritative context passed to <see cref="IStationActionHandler"/> implementations.</summary>
public readonly struct StationActionContext
{
    /// <summary>Creates the immutable execution context for one sequenced station action op.</summary>
    public StationActionContext(
        DataStore store,
        int player,
        Uuid playerUuid,
        Uuid clientId,
        uint sequenceNumber,
        in StationActionOp op,
        GameMode gameMode,
        long simTick = 0L,
        float fixedDelta = 0f,
        IStationSession? stations = null
    )
    {
        Store = store;
        Player = player;
        PlayerUuid = playerUuid;
        ClientId = clientId;
        SequenceNumber = sequenceNumber;
        Op = op;
        GameMode = gameMode;
        SimTick = simTick;
        FixedDelta = fixedDelta;
        Stations = stations;
    }

    /// <summary>The world store the action executes against.</summary>
    public readonly DataStore Store;

    /// <summary>The acting player's ECS entity id.</summary>
    public readonly int Player;

    /// <summary>The acting player's stable entity uuid.</summary>
    public readonly Uuid PlayerUuid;

    /// <summary>The acting player's stable session client id.</summary>
    public readonly Uuid ClientId;

    /// <summary>The deduplicated client sequence number of this command.</summary>
    public readonly uint SequenceNumber;

    /// <summary>The station command payload.</summary>
    public readonly StationActionOp Op;

    /// <summary>The acting player's authoritative game mode.</summary>
    public readonly GameMode GameMode;

    /// <summary>The authoritative server sim tick.</summary>
    public readonly long SimTick;

    /// <summary>The fixed simulation timestep in seconds.</summary>
    public readonly float FixedDelta;

    /// <summary>The per-world station lifecycle and authorization session, or null in minimal tests.</summary>
    public readonly IStationSession? Stations;
}
