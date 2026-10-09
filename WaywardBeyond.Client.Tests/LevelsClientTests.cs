using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Data;
using WaywardBeyond.Networking.Transport;
using NUnit.Framework;

namespace WaywardBeyond.Client.Tests;

/// <summary>
/// Level management requests must not strand a waiter when no transport is active. A request issued
/// before a remote client connects (the startup save-list refresh) must fail fast and leave nothing
/// queued, otherwise its waiter consumes the first real response and every later refresh stalls. A
/// level created through the facade must then be visible to the next list refresh.
/// </summary>
public class LevelsClientTests
{
    [Test]
    public async Task FailedSendDoesNotStrandWaiter()
    {
        var connection = new ScriptedConnection { Connected = false };
        var client = new LevelsClient(connection);

        Task<Level[]> beforeConnect = client.GetLevelsAsync();
        Assert.That(beforeConnect.IsCompleted, Is.True, "A request with no active transport must fail fast.");
        Assert.That(await beforeConnect, Is.Empty);

        connection.Connected = true;

        Task<Level[]> afterConnect = client.GetLevelsAsync();
        client.Poll();

        Assert.That(afterConnect.IsCompleted, Is.True, "The response must reach the request that was actually sent.");
        Assert.That(afterConnect.Result, Is.Empty, "No levels exist yet.");
    }

    [Test]
    public async Task CreateThenListSeesTheNewLevel()
    {
        var connection = new ScriptedConnection { Connected = false };
        var client = new LevelsClient(connection);

        //  The startup refresh fires before any transport exists; it must not poison the response queue.
        Task<Level[]> startup = client.GetLevelsAsync();
        Assert.That(await startup, Is.Empty);

        connection.Connected = true;

        Task<bool> create = client.CreateLevelAsync("New Save", "seed", GameMode.Creative);
        client.Poll();
        Assert.That(create.IsCompleted, Is.True, "The create response must complete the request.");
        Assert.That(create.Result, Is.True);

        Task<Level[]> list = client.GetLevelsAsync();
        client.Poll();
        Assert.That(list.IsCompleted, Is.True, "The list response must complete the request.");

        Level[] levels = list.Result;
        Assert.That(levels, Has.Length.EqualTo(1));
        Assert.That(levels[0].Name, Is.EqualTo("New Save"));
    }

    /// <summary>An in-memory server stand-in: requests sent while connected are answered immediately.</summary>
    private sealed class ScriptedConnection : IClientConnection
    {
        private readonly Dictionary<Type, Queue<object>> _inbox = [];
        private readonly List<Level> _levels = [];

        public bool Connected { get; set; }

        public bool IsConnected => Connected;

        public bool IsLocal => false;

        public Result Send<T>(in T message)
        {
            if (!Connected)
            {
                return Result.FromFailure("No active connection.");
            }

            switch (message)
            {
                case NewLevelRequest create:
                {
                    var level = new Level { Guid = Guid.NewGuid().ToString("N"), Name = create.Name ?? string.Empty };
                    _levels.Add(level);
                    Enqueue(new NewLevelResponse { LevelGuid = level.Guid, Success = true });
                    break;
                }
                case ListLevelsRequest:
                    Enqueue(new ListLevelsResponse { Levels = _levels.ToArray() });
                    break;
                case DeleteLevelRequest delete:
                    _levels.RemoveAll(level => level.Guid == delete.LevelGuid);
                    Enqueue(new DeleteLevelResponse { Success = true });
                    break;
                case SaveLevelRequest:
                    Enqueue(new SaveLevelResponse { Success = true });
                    break;
            }

            return Result.FromSuccess();
        }

        public Result<T> Receive<T>()
        {
            if (_inbox.TryGetValue(typeof(T), out Queue<object>? queue) && queue.Count > 0)
            {
                return Result<T>.FromSuccess((T)queue.Dequeue());
            }

            return Result<T>.FromFailure("No messages available.");
        }

        private void Enqueue<T>(T message)
        {
            if (!_inbox.TryGetValue(typeof(T), out Queue<object>? queue))
            {
                queue = new Queue<object>();
                _inbox[typeof(T)] = queue;
            }

            queue.Enqueue(message!);
        }
    }
}
