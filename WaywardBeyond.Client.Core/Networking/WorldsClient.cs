using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Networking;

/// <summary>
/// Client half of the server-owned world management. The menu issues request messages against the
/// in-process server (list/create/delete/save a world) and awaits the matching response, which is
/// delivered asynchronously and completed by <see cref="Poll"/> - driven on the client ECS thread by a
/// dedicated system, so the menu never blocks a thread spinning on the transport. Responses are matched
/// to requests strictly in FIFO order per response type, which is correct here because the menu issues
/// at most one outstanding operation of each kind at a time.
/// </summary>
internal sealed class WorldsClient
{
    private readonly IClientConnection _transport;
    private readonly object _gate = new();
    private readonly Dictionary<Type, Queue<Action<object>>> _pending = [];

    public WorldsClient(in IClientConnection transport)
    {
        _transport = transport;
    }

    /// <summary>Drains every in-flight world-management response and completes its waiter. Run on the ECS thread.</summary>
    public void Poll()
    {
        Drain<ListWorldsResponse>();
        Drain<NewWorldResponse>();
        Drain<DeleteWorldResponse>();
        Drain<SaveWorldResponse>();
    }

    public Task<Level[]> GetLevelsAsync()
    {
        var completion = new TaskCompletionSource<Level[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        Request(new ListWorldsRequest { Dummy = 0 }, (ListWorldsResponse response) => completion.TrySetResult(response.Levels ?? []));
        return completion.Task;
    }

    public Task<bool> CreateWorldAsync(string name, string seed, GameMode gameMode)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Request(
            new NewWorldRequest { Name = name, Seed = seed, GameMode = (int)gameMode },
            (NewWorldResponse response) => completion.TrySetResult(response.Success)
        );
        return completion.Task;
    }

    public Task<bool> DeleteWorldAsync(string levelGuid)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Request(
            new DeleteWorldRequest { LevelGuid = levelGuid },
            (DeleteWorldResponse response) => completion.TrySetResult(response.Success)
        );
        return completion.Task;
    }

    public Task<bool> SaveWorldAsync()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Request(
            new SaveWorldRequest { Dummy = 0 },
            (SaveWorldResponse response) => completion.TrySetResult(response.Success)
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
        Action<object>? onComplete = null;
        lock (_gate)
        {
            if (_pending.TryGetValue(typeof(TResponse), out Queue<Action<object>>? queue) && queue.Count > 0)
            {
                onComplete = queue.Dequeue();
            }
        }

        onComplete?.Invoke(response!);
    }

    private void Request<TRequest, TResponse>(TRequest request, Action<TResponse> onComplete)
        where TRequest : struct
    {
        lock (_gate)
        {
            if (!_pending.TryGetValue(typeof(TResponse), out Queue<Action<object>>? queue))
            {
                queue = new Queue<Action<object>>();
                _pending[typeof(TResponse)] = queue;
            }

            queue.Enqueue(response => onComplete((TResponse)response!));
        }

        _transport.Send(request);
    }
}