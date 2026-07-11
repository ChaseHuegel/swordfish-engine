using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Swordfish.Library.Collections.Filtering;
using Swordfish.Library.Threading;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking.Events;
using WaywardBeyond.Shared.Networking.Sessions;

namespace WaywardBeyond.Shared.Networking.Transport;

public abstract class FrameStreamService(in SessionService sessionService) : IDataReceiver, IDataSender
{
    public event EventHandler<DataEventArgs>? Received;
    private readonly SessionService _sessionService = sessionService;
    private readonly ConcurrentDictionary<Session, FrameStream> _connections = new();

    public Result Send(byte[] data, Session target)
    {
        if (!_connections.TryGetValue(target, out FrameStream? peer))
        {
            return Result.FromFailure($"Unknown session: {target}.");
        }
        
        return peer.WriteFrame(data);
    }

    public Result Send(byte[] data, IFilter<Session> targetFilter)
    {
        bool success = true;
        StringBuilder? errorMessage = null;
        foreach (KeyValuePair<Session, FrameStream> connection in _connections)
        {
            if (!targetFilter.Allowed(connection.Key))
            {
                continue;
            }
            
            Result send = Send(data, connection.Key);
            if (!send)
            {
                success = false;
                errorMessage ??= new StringBuilder();
                errorMessage.AppendLine($"Failed to send data to session: {connection.Key}, message: {send.Message}");
            }
        }
        
        return new Result(success, errorMessage?.ToString() ?? null);
    }

    protected Session AcceptPeer(FrameStream frameStream)
    {
        Result<Session> result = _sessionService.RequestNew();
        Session session = result;
        _connections.TryAdd(session, frameStream);
        
        var worker = new ThreadWorker(() => ListenToPeer(session, frameStream), $"{GetType().Name}.Peer.{session}");
        worker.Start();
        return session;
    }

    private void ListenToPeer(Session session, FrameStream frameStream)
    {
        try
        {
            while (_sessionService.Validate(session))
            {
                Result<byte[]> readResult = frameStream.ReadFrame();
                if (!readResult)
                {
                    break;
                }
                
                Received?.Invoke(this, new DataEventArgs(readResult, session));
            }
        }
        finally
        {
            _sessionService.End(session);
            _connections.TryRemove(session, out _);
            frameStream.Dispose();
        }
    }
}
