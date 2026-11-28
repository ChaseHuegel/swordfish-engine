using System;
using System.Collections.Concurrent;
using System.Threading;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Thread-safe bridge between the ECS-replicated <see cref="ActiveStationComponent"/> and client UI layers.
/// Mirrors server state without client prediction and queues outbound <see cref="StationActionOp"/> commands
/// for the ECS thread to stage.
/// </summary>
internal sealed class ClientStationState
{
    private readonly Lock _stateLock = new();
    private readonly ConcurrentQueue<StationActionOp> _pendingOutbound = new();
    private ActiveStationComponent _current = ActiveStationComponent.None;

    /// <summary>Raised outside internal locks whenever the local player's replicated station binding changes.</summary>
    public event Action<ActiveStationComponent, ActiveStationComponent>? Changed;

    /// <summary>The latest server-replicated active station binding for the local player.</summary>
    public ActiveStationComponent Current
    {
        get
        {
            lock (_stateLock)
            {
                return _current;
            }
        }
    }

    /// <summary>Whether any station interface is currently bound by the server.</summary>
    public bool HasOpenStation => Current.IsOpen;

    /// <summary>Whether a contextual <c>[Tab]</c> station is currently bound by the server.</summary>
    public bool IsContextualTabOpen
    {
        get
        {
            ActiveStationComponent current = Current;
            return current.IsOpen && current.Kind == StationUiKind.ContextualTab;
        }
    }

    /// <summary>Whether a WAY-OS terminal station is currently bound by the server.</summary>
    public bool IsTerminalOpen
    {
        get
        {
            ActiveStationComponent current = Current;
            return current.IsOpen && current.Kind == StationUiKind.Terminal;
        }
    }

    /// <summary>Enqueues an outbound server-authoritative station command from any thread.</summary>
    public void EnqueueAction(in StationActionOp op)
    {
        _pendingOutbound.Enqueue(op);
    }

    /// <summary>Enqueues a <see cref="StationActionKind.CloseStation"/> command for the currently open station.</summary>
    public void EnqueueCloseStation()
    {
        ulong stationEntity = Current.StationEntity;
        _pendingOutbound.Enqueue(new StationActionOp(
            _TargetEntity: stationEntity,
            _Action: StationActionKind.CloseStation,
            _ActionId: null,
            _Argument: null,
            _Count: 0u));
    }

    /// <summary>Applies a newly observed replicated <see cref="ActiveStationComponent"/> and fires <see cref="Changed"/> on transitions.</summary>
    internal void UpdateReplicatedState(in ActiveStationComponent next)
    {
        ActiveStationComponent previous;
        lock (_stateLock)
        {
            if (_current.EqualsState(in next))
            {
                return;
            }

            previous = _current;
            _current = next;
        }

        Changed?.Invoke(previous, next);
    }

    /// <summary>Attempts to dequeue the next pending outbound station command on the ECS thread.</summary>
    internal bool TryDequeueOutbound(out StationActionOp op)
    {
        return _pendingOutbound.TryDequeue(out op);
    }

    /// <summary>Clears any queued outbound commands and resets the active station binding on disconnect or menu exit.</summary>
    internal void Reset()
    {
        while (_pendingOutbound.TryDequeue(out _))
        {
        }

        UpdateReplicatedState(ActiveStationComponent.None);
    }
}
