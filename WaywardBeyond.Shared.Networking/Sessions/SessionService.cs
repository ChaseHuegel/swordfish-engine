using System.Collections.Generic;
using System.Threading;
using Swordfish.Library.Util;

namespace WaywardBeyond.Shared.Networking.Sessions;

public class SessionService
{
    private uint _lastSessionID;

    private readonly Dictionary<uint, Session> _sessions = [];
    private readonly object _sessionsLock = new();

    public Result<Session> RequestNew()
    {
        uint id = Interlocked.Increment(ref _lastSessionID);

        var session = new Session(id);
        lock (_sessionsLock)
        {
            bool added = _sessions.TryAdd(id, session);
            return new Result<Session>(added, session);
        }
    }

    public Result<Session> End(Session session)
    {
        lock (_sessionsLock)
        {
            bool removed = _sessions.Remove(session.ID);
            return new Result<Session>(removed, session);
        }
    }

    public Result<Session> Validate(Session session)
    {
        lock (_sessionsLock)
        {
            bool exists = _sessions.ContainsKey(session.ID);
            return new Result<Session>(exists, session);
        }
    }

    public Result<Session> Get(uint id)
    {
        lock (_sessionsLock)
        {
            bool exists = _sessions.TryGetValue(id, out Session session);
            return new Result<Session>(exists, session);
        }
    }
}
