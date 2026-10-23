using System;
using System.Collections.Generic;
using System.Threading;
using NsdRpc;

namespace WaywardBeyond.Networking.Transport;

/// <summary>
/// One side of an in-process loopback link. The endpoint copies every sent frame into the peer's
/// inbox. Frames that arrive before the peer subscribes to <see cref="FrameReceived"/> wait there, so
/// a hello sent before the RPC peer is wired is not lost. Counters and the disconnect event belong to
/// this side only.
/// </summary>
internal sealed class LoopbackRpcEndpoint
{
    private readonly Lock _gate = new();
    private readonly Lock _deliveryGate = new();
    private readonly Queue<byte[]> _pending = [];
    private RpcFrameHandler? _frameReceived;
    private Action<RpcDisconnectReason>? _disconnected;
    private LoopbackRpcEndpoint? _peer;
    private bool _connected;
    private bool _closed;

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
            //  Hold the delivery gate while subscribing and draining the buffered frames, so a frame
            //  sent during the subscription cannot overtake the frames already waiting.
            lock (_deliveryGate)
            {
                List<byte[]> buffered;
                lock (_gate)
                {
                    _frameReceived += value;
                    if (_pending.Count == 0)
                    {
                        return;
                    }

                    buffered = [.. _pending];
                    _pending.Clear();
                }

                foreach (byte[] frame in buffered)
                {
                    value(frame);
                }
            }
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

    /// <summary>Accepts one copied frame from the peer, buffering it until a subscriber exists.</summary>
    public bool Deliver(byte[] frame)
    {
        RpcFrameHandler? handler;
        lock (_gate)
        {
            if (_closed)
            {
                return false;
            }

            Interlocked.Increment(ref _packetsReceived);
            Interlocked.Add(ref _bytesReceived, frame.Length);

            if (_frameReceived == null)
            {
                _pending.Enqueue(frame);
                return true;
            }

            handler = _frameReceived;
        }

        DeliverToSubscriber(handler, frame);
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
            _pending.Clear();
            handler = _disconnected;
            _disconnected = null;
            peer = _peer;
        }

        handler?.Invoke(reason);
        peer?.Close(RpcDisconnectReason.PeerClosed);
    }

    private void DeliverToSubscriber(RpcFrameHandler handler, byte[] frame)
    {
        //  Serialize deliveries so one subscriber sees frames in send order.
        lock (_deliveryGate)
        {
            handler(frame);
        }
    }
}
