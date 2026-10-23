using System;
using System.Collections.Generic;
using System.Threading;
using NsdRpc;

namespace WaywardBeyond.Networking.Transport;

/// <summary>One side of an in-process loopback link. Frames are delivered in send order.</summary>
internal sealed class LoopbackRpcEndpoint
{
    private const int MAX_PENDING_BYTES = 1 << 20;

    private readonly Lock _gate = new();
    private readonly Queue<byte[]> _pending = [];
    private RpcFrameHandler? _frameReceived;
    private Action<RpcDisconnectReason>? _disconnected;
    private LoopbackRpcEndpoint? _peer;
    private int _pendingBytes;
    private int _drainThreadId;
    private bool _connected;
    private volatile bool _closed;

    private long _packetsSent;
    private long _packetsReceived;
    private long _bytesSent;
    private long _bytesReceived;
    private long _rejectedFrames;

    public bool IsConnected
    {
        get
        {
            lock (_gate)
            {
                return _connected && !_closed;
            }
        }
    }

    public RpcConnectionStats Stats => new(
        packetsSent: Interlocked.Read(ref _packetsSent),
        packetsReceived: Interlocked.Read(ref _packetsReceived),
        bytesSent: Interlocked.Read(ref _bytesSent),
        bytesReceived: Interlocked.Read(ref _bytesReceived),
        queuedBytes: 0,
        peakQueuedBytes: 0,
        writes: Interlocked.Read(ref _packetsSent),
        rejectedFrames: Interlocked.Read(ref _rejectedFrames)
    );

    public event Action<RpcDisconnectReason> Disconnected
    {
        add
        {
            lock (_gate)
            {
                _disconnected += value;
            }
        }
        remove
        {
            lock (_gate)
            {
                _disconnected -= value;
            }
        }
    }

    public event RpcFrameHandler FrameReceived
    {
        add
        {
            lock (_gate)
            {
                _frameReceived += value;
            }

            //  Deliver frames that arrived before the subscriber existed.
            Drain();
        }
        remove
        {
            lock (_gate)
            {
                _frameReceived -= value;
            }
        }
    }

    /// <summary>Pairs this endpoint with its peer and marks both sides connected.</summary>
    public void Pair(LoopbackRpcEndpoint peer)
    {
        lock (_gate)
        {
            _peer = peer;
            _connected = true;
        }
    }

    public bool TrySend(ReadOnlySpan<byte> frame)
    {
        LoopbackRpcEndpoint? peer;
        lock (_gate)
        {
            if (!_connected || _closed || _peer == null)
            {
                Interlocked.Increment(ref _rejectedFrames);
                return false;
            }

            peer = _peer;
        }

        byte[] copy = frame.ToArray();
        if (!peer.Deliver(copy))
        {
            Interlocked.Increment(ref _rejectedFrames);
            return false;
        }

        Interlocked.Increment(ref _packetsSent);
        Interlocked.Add(ref _bytesSent, copy.Length);
        return true;
    }

    /// <summary>Accepts one copied frame from the peer and delivers it in order.</summary>
    public bool Deliver(byte[] frame)
    {
        var overflow = false;
        lock (_gate)
        {
            if (_closed)
            {
                return false;
            }

            Interlocked.Increment(ref _packetsReceived);
            Interlocked.Add(ref _bytesReceived, frame.Length);

            _pending.Enqueue(frame);
            _pendingBytes += frame.Length;
            overflow = _pendingBytes > MAX_PENDING_BYTES;
        }

        if (overflow)
        {
            //  A peer that outruns its subscriber by this much is not going to recover.
            Close(RpcDisconnectReason.ProtocolViolation);
            return false;
        }

        Drain();
        return true;
    }

    /// <summary>Closes this side once, reports the reason, then tells the peer its side left.</summary>
    public void Close(RpcDisconnectReason reason)
    {
        Action<RpcDisconnectReason>? handler;
        LoopbackRpcEndpoint? peer;
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            _connected = false;
            _frameReceived = null;
            _pending.Clear();
            _pendingBytes = 0;
            handler = _disconnected;
            _disconnected = null;
            peer = _peer;
        }

        handler?.Invoke(reason);
        peer?.Close(RpcDisconnectReason.PeerClosed);
    }

    private void Drain()
    {
        lock (_gate)
        {
            if (_closed || _drainThreadId != 0 || _pending.Count == 0)
            {
                return;
            }

            _drainThreadId = Environment.CurrentManagedThreadId;
        }

        try
        {
            while (true)
            {
                byte[] frame;
                RpcFrameHandler? handler;
                lock (_gate)
                {
                    if (_closed || _frameReceived == null || _pending.Count == 0)
                    {
                        _drainThreadId = 0;
                        return;
                    }

                    frame = _pending.Dequeue();
                    _pendingBytes -= frame.Length;
                    handler = _frameReceived;
                }

                handler(frame);
            }
        }
        finally
        {
            lock (_gate)
            {
                if (_drainThreadId == Environment.CurrentManagedThreadId)
                {
                    _drainThreadId = 0;
                }
            }
        }
    }
}
