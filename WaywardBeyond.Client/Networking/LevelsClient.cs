using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Transport;

namespace WaywardBeyond.Client.Networking;

/// <summary>
/// Client half of the server-owned level management. The menu issues request messages against the
/// in-process server (list/create/delete/save a level) and awaits the matching response, which is
/// delivered asynchronously and completed by <see cref="Poll"/> - driven on the client ECS thread by a
/// dedicated system, so the menu never blocks a thread spinning on the transport. Responses are matched
/// to requests strictly in FIFO order per response type, which is correct here because the menu issues
/// at most one outstanding operation of each kind at a time. A dropped connection faults every pending
/// operation (<see cref="FaultPending"/>) so no waiter hangs on a vanished server.
/// </summary>
internal sealed class LevelsClient
{
    private readonly IClientConnection _transport;
    private readonly object _gate = new();
    private readonly Dictionary<Type, Queue<(Action<object> onComplete, Action onFailure)>> _pending = [];

    public LevelsClient(in IClientConnection transport)
    {
        _transport = transport;
    }

    /// <summary>Drains every in-flight level-management response and completes its waiter. Run on the ECS thread.</summary>
    public void Poll()
    {
        Drain<ListLevelsResponse>();
        Drain<NewLevelResponse>();
        Drain<DeleteLevelResponse>();
        Drain<SaveLevelResponse>();
    }

    /// <summary>
    /// Faults every pending operation (called on connection drop): each waiter completes with its
    /// failure value instead of awaiting a response that will never come. Run on the ECS thread.
    /// </summary>
    public void FaultPending()
    {
        lock (_gate)
        {
            foreach (Queue<(Action<object>, Action)> queue in _pending.Values)
            {
                while (queue.Count > 0)
                {
                    queue.Dequeue().Item2();
                }
            }

            _pending.Clear();
        }
    }

    public Task<Level[]> GetLevelsAsync()
    {
        var completion = new TaskCompletionSource<Level[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        Request(
            new ListLevelsRequest { Dummy = 0 },
            (ListLevelsResponse response) => completion.TrySetResult(response.Levels ?? []),
            () => completion.TrySetResult([])
        );
        return completion.Task;
    }

    public Task<bool> CreateLevelAsync(string name, string seed, GameMode gameMode)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Request(
            new NewLevelRequest { Name = name, Seed = seed, GameMode = (int)gameMode },
            (NewLevelResponse response) => completion.TrySetResult(response.Success),
            () => completion.TrySetResult(false)
        );
        return completion.Task;
    }

    public Task<bool> DeleteLevelAsync(string levelGuid)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Request(
            new DeleteLevelRequest { LevelGuid = levelGuid },
            (DeleteLevelResponse response) => completion.TrySetResult(response.Success),
            () => completion.TrySetResult(false)
        );
        return completion.Task;
    }

    public Task<bool> SaveLevelAsync()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Request(
            new SaveLevelRequest { Dummy = 0 },
            (SaveLevelResponse response) => completion.TrySetResult(response.Success),
            () => completion.TrySetResult(false)
        );
        return completion.Task;
    }

    /// <summary>
    /// Fire-and-forget notification to the server that the player is returning to the menu, so it can
    /// end the session and free the player mirror. There is no response to await.
    /// </summary>
    public void SendLeaveGame()
    {
        _transport.Send(new LeaveGameRequest { Dummy = 0 });
    }

    private void Drain<TResponse>()
    {
        Result<TResponse> result;
        while ((result = _transport.Receive<TResponse>()).Success)
        {
            Complete(result.Value);
        }
    }

    private void Complete<TResponse>(TResponse response)
    {
        (Action<object> onComplete, Action _)? waiter = null;
        lock (_gate)
        {
            if (_pending.TryGetValue(typeof(TResponse), out Queue<(Action<object>, Action)>? queue) && queue.Count > 0)
            {
                waiter = queue.Dequeue();
            }
        }

        waiter?.Item1(response!);
    }

    private void Request<TRequest, TResponse>(TRequest request, Action<TResponse> onComplete, Action onFailure)
        where TRequest : struct
    {
        lock (_gate)
        {
            if (!_pending.TryGetValue(typeof(TResponse), out Queue<(Action<object>, Action)>? queue))
            {
                queue = new Queue<(Action<object>, Action)>();
                _pending[typeof(TResponse)] = queue;
            }

            queue.Enqueue((response => onComplete((TResponse)response!), onFailure));
        }

        _transport.Send(request);
    }
}