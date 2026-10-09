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
/// to requests strictly in FIFO order per response type. A send is only queued when it succeeds; a
/// failed send cancels its waiter (<see cref="Request{TRequest, TResponse}"/>) so a request issued
/// before a transport exists cannot consume a later response. A dropped connection faults every pending
/// operation (<see cref="FaultPending"/>) so no waiter hangs on a vanished server.
/// </summary>
internal sealed class LevelsClient
{
    private readonly IClientConnection _transport;
    private readonly object _gate = new();
    private readonly Dictionary<Type, Queue<Waiter>> _pending = [];

    /// <summary>A single outstanding request's completion callbacks. Canceled waiters are skipped when a
    /// response arrives, so they can never be matched to a response they did not request.</summary>
    private sealed class Waiter(Action<object> onComplete, Action onFailure)
    {
        public readonly Action<object> OnComplete = onComplete;
        public readonly Action OnFailure = onFailure;
        public bool Canceled;
    }

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
            foreach (Queue<Waiter> queue in _pending.Values)
            {
                while (queue.Count > 0)
                {
                    queue.Dequeue().OnFailure();
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
        Waiter? waiter = null;
        lock (_gate)
        {
            if (_pending.TryGetValue(typeof(TResponse), out Queue<Waiter>? queue))
            {
                while (queue.Count > 0)
                {
                    Waiter candidate = queue.Dequeue();
                    if (candidate.Canceled)
                    {
                        continue;
                    }

                    waiter = candidate;
                    break;
                }
            }
        }

        waiter?.OnComplete(response!);
    }

    private void Request<TRequest, TResponse>(TRequest request, Action<TResponse> onComplete, Action onFailure)
        where TRequest : struct
    {
        var waiter = new Waiter(response => onComplete((TResponse)response!), onFailure);
        lock (_gate)
        {
            if (!_pending.TryGetValue(typeof(TResponse), out Queue<Waiter>? queue))
            {
                queue = new Queue<Waiter>();
                _pending[typeof(TResponse)] = queue;
            }

            queue.Enqueue(waiter);
        }

        Result send = _transport.Send(request);
        if (send.Success)
        {
            return;
        }

        //  The request never left: cancel the waiter so it cannot consume a response meant for another
        //  request, and let the caller fail fast instead of awaiting forever.
        lock (_gate)
        {
            waiter.Canceled = true;
        }

        onFailure();
    }
}