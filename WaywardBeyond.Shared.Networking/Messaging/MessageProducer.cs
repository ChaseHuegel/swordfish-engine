using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Swordfish.Library.Collections.Filtering;
using Swordfish.Library.Serialization;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Sessions;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Shared.Networking.Messaging;

public class MessageProducer<T> : IMessageProducer<T>, IDisposable
{
    protected readonly ISerializer<T> _serializer;
    protected readonly GamePacketType _packetType;
    private IDataSender[] _senders;
    private uint _sequenceNumber;
    private bool _disposed;

    public MessageProducer(ISerializer<T> serializer, IDataSender[] senders, GamePacketType packetType)
    {
        List<IDataSender> matchingSenders = [];
        for (int i = 0; i < senders.Length; i++)
        {
            matchingSenders.Add(senders[i]);
        }
        _serializer = serializer;
        _senders = [.. matchingSenders];
        _packetType = packetType;
    }

    public Result Send(T message, Session target)
    {
        byte[] payload = _serializer.Serialize(message);
        uint sequence = Interlocked.Increment(ref _sequenceNumber);
        var gamePacket = new GamePacket(sequence, 0, _packetType, payload);
        byte[] packetBytes = gamePacket.Serialize();

        bool success = true;
        StringBuilder? errorMessage = null;
        for (int i = 0; i < _senders.Length; i++)
        {
            Result sendResult = _senders[i].Send(packetBytes, target);
            success &= sendResult.Success;
            if (!success)
            {
                errorMessage ??= new StringBuilder();
                errorMessage.AppendLine(sendResult.Message);
            }
        }
        return new Result(success, errorMessage?.ToString() ?? null);
    }

    public Result Send(T message, IFilter<Session> targetFilter)
    {
        byte[] payload = _serializer.Serialize(message);
        uint sequence = Interlocked.Increment(ref _sequenceNumber);
        var gamePacket = new GamePacket(sequence, 0, _packetType, payload);
        byte[] packetBytes = gamePacket.Serialize();

        bool success = true;
        StringBuilder? errorMessage = null;
        for (int i = 0; i < _senders.Length; i++)
        {
            Result sendResult = _senders[i].Send(packetBytes, targetFilter);
            success &= sendResult.Success;
            if (!success)
            {
                errorMessage ??= new StringBuilder();
                errorMessage.AppendLine(sendResult.Message);
            }
        }
        return new Result(success, errorMessage?.ToString() ?? null);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _senders = null!;
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~MessageProducer()
    {
        Dispose(false);
    }
}
