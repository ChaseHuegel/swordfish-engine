using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Serialization;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking.Serialization;

namespace WaywardBeyond.Shared.Networking.Transport;

/// <summary>
/// Socket transport usable as either a client (<see cref="Connect"/>) or a server peer
/// (<see cref="Listen"/>). Every message is serialized through the wire format and framed with a
/// length prefix plus a type tag, then dispatched to a per-type queue on the receiving side — so a
/// peer can poll for several distinct message kinds independently without one polling loop stealing
/// another's frames. Transport framing only: no ack, ordering, or reliability.
/// </summary>
public sealed class TcpTransport : IClientConnection, IServerConnection, IDisposable
{
    private readonly SerializerCache _serializers;
    private readonly ILogger _logger;
    private readonly int _connectionTimeoutMs;
    private readonly int _keepaliveIntervalMs;
    private readonly int _sendQueueSize;
    private readonly int _maxFrameBytes;
    private readonly ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> _receiveQueues = new();
    private readonly BlockingCollection<byte[]> _sendQueue;
    private readonly BlockingCollection<byte[]> _reliableQueue = new();
    private readonly int _reliableConcernThreshold;
    private readonly int _reliableDisconnectThreshold;
    private readonly int _reliableDisconnectMs;
    private int _reliableConcernLoggedCount;
    private int _reliableOverflowTicks;
    private readonly CancellationTokenSource _sendCts = new();
    private readonly CancellationTokenSource _keepaliveCts = new();
    private TcpClient? _client;
    private TcpListener? _listener;
    private NetworkStream? _stream;
    private volatile bool _isRunning;
    private volatile bool _disconnectedRaised;
    private Thread? _receiveThread;
    private Thread? _sendThread;
    private Thread? _keepaliveThread;

    //  Keepalive frame with an empty type tag: [frameLen=4][typeTagLen=0], so a live-but-idle peer always
    //  delivers a readable byte within the socket timeout and is never falsely dropped.
    private static readonly byte[] _KEEPALIVE_FRAME = [0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    private const int _THREAD_JOIN_TIMEOUT_MS = 2000;

    /// <summary>
    /// True as long as the receive and send loops are running: false once the peer is gone (read/send
    /// failure) or the transport was intentionally disconnected. Not the socket's stale
    /// <c>TcpClient.Connected</c> result, which reflects only the last I/O.
    /// </summary>
    public bool IsConnected => _isRunning;
    public bool IsLocal => false;

    /// <summary>
    /// Raised once when the remote peer disconnects (receive loop reaches EOF/error) while the transport
    /// is still running — i.e. not on an intentional <see cref="Disconnect"/>. Used by the server host to
    /// drop a departed client from its connection hub, and by the owner (LanHost, TransportManager) to
    /// dispose the transport exactly once.
    /// </summary>
    public event Action? OnDisconnected;

    public TcpTransport(
        IEnumerable<INetworkSerializer> serializers,
        ILoggerFactory? loggerFactory = null,
        int connectionTimeoutMs = 5000,
        int sendQueueSize = 256,
        int keepaliveIntervalMs = 2000,
        int maxFrameBytes = 16 * 1024 * 1024,
        int reliableQueueConcernThreshold = 64,
        int reliableQueueDisconnectThreshold = 128,
        int reliableQueueDisconnectMs = 10_000
    ) {
        _serializers = new SerializerCache(serializers);
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<TcpTransport>();
        _connectionTimeoutMs = connectionTimeoutMs;
        _keepaliveIntervalMs = ClampKeepaliveInterval(keepaliveIntervalMs, connectionTimeoutMs);
        _sendQueueSize = Math.Max(1, sendQueueSize);
        _maxFrameBytes = Math.Max(64, maxFrameBytes);
        _reliableConcernThreshold = Math.Max(1, reliableQueueConcernThreshold);
        _reliableDisconnectThreshold = Math.Max(_reliableConcernThreshold, reliableQueueDisconnectThreshold);
        _reliableDisconnectMs = Math.Max(1, reliableQueueDisconnectMs);
        _sendQueue = new BlockingCollection<byte[]>(_sendQueueSize);
    }

    /// <summary>
    /// Builds a transport that owns an already-accepted socket (used by the server host for each peer).
    /// </summary>
    public static TcpTransport Accepted(
        IEnumerable<INetworkSerializer> serializers,
        TcpClient client,
        ILoggerFactory? loggerFactory = null,
        int connectionTimeoutMs = 5000,
        int sendQueueSize = 256,
        int keepaliveIntervalMs = 2000,
        int maxFrameBytes = 16 * 1024 * 1024,
        int reliableQueueConcernThreshold = 64,
        int reliableQueueDisconnectThreshold = 128,
        int reliableQueueDisconnectMs = 10_000
    ) {
        var transport = new TcpTransport(serializers, loggerFactory, connectionTimeoutMs, sendQueueSize, keepaliveIntervalMs, maxFrameBytes, reliableQueueConcernThreshold, reliableQueueDisconnectThreshold, reliableQueueDisconnectMs);
        transport._client = client;
        transport._client.NoDelay = true;
        transport._client.SendTimeout = connectionTimeoutMs;
        transport._client.ReceiveTimeout = connectionTimeoutMs;
        transport._stream = client.GetStream();
        transport.StartLoops();
        return transport;
    }

    /// <summary>The bound local port after <see cref="Listen"/>, or 0 if not listening.</summary>
    public int LocalPort => (_listener?.LocalEndpoint as IPEndPoint)?.Port ?? 0;

    public void Connect(string host, int port)
    {
        _client = new TcpClient();
        try
        {
            //  A synchronous connect has no timeout of its own: against a blackholed route it waits out
            //  the OS retry schedule (tens of seconds). Bounding the async connect keeps the calling
            //  thread (the multiplayer UI path) responsive within ConnectionTimeoutMs.
            Task connect = _client.ConnectAsync(host, port);
            if (!connect.Wait(_connectionTimeoutMs))
            {
                throw new TimeoutException($"Connecting to {host}:{port} timed out after {_connectionTimeoutMs} ms.");
            }

            connect.GetAwaiter().GetResult();
            _client.NoDelay = true;
            _client.SendTimeout = _connectionTimeoutMs;
            _client.ReceiveTimeout = _connectionTimeoutMs;
            _stream = _client.GetStream();
            StartLoops();
        }
        catch
        {
            //  A failed or abandoned connect must not leak the socket.
            _client.Close();
            throw;
        }
    }

    public void Listen(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        var acceptThread = new Thread(AcceptLoop)
        {
            IsBackground = true,
            Name = "TcpTransport Accept"
        };
        acceptThread.Start();
    }

    private void AcceptLoop()
    {
        try
        {
            _client = _listener!.AcceptTcpClient();
            _client.NoDelay = true;
            _client.SendTimeout = _connectionTimeoutMs;
            _client.ReceiveTimeout = _connectionTimeoutMs;
            _stream = _client.GetStream();
            StartLoops();
        }
        catch
        {
            //  Listener stopped (Dispose/Disconnect) while waiting for a connection.
        }
    }

    public void Disconnect()
    {
        _isRunning = false;
        _sendCts.Cancel();
        _keepaliveCts.Cancel();
        _stream?.Close();
        _client?.Close();
        _listener?.Stop();
    }

    public Result Send<T>(in T message)
    {
        if (!_isRunning)
        {
            return Result.FromFailure("Transport is not running.");
        }
        if (!_serializers.TryGet<T>(out ISerializer<T> serializer))
        {
            return Result.FromFailure($"No serializer registered for type {typeof(T).Name}.");
        }
        if (!_serializers.TryGetTypeName<T>(out string typeName))
        {
            return Result.FromFailure($"No serializer registered for type {typeof(T).Name}.");
        }

        byte[] payload = serializer.Serialize(message);
        byte[] typeTag = Encoding.UTF8.GetBytes(typeName);

        //  Frame body = [4-byte type-tag length][type tag][payload], preceded on the wire by a
        //  [4-byte body length] prefix (the body length excludes the prefix itself).
        byte[] frame = new byte[4 + typeTag.Length + payload.Length];
        BitConverter.TryWriteBytes(frame.AsSpan(0, 4), typeTag.Length);
        typeTag.CopyTo(frame, 4);
        payload.CopyTo(frame, 4 + typeTag.Length);

        var bytes = new byte[4 + frame.Length];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), frame.Length);
        frame.CopyTo(bytes, 4);

        //  Refuse frames over the negotiated cap so the receive side never sees a prefix it must drop a
        //  peer for. Serialization already happened; we only skip the queue.
        if (frame.Length > _maxFrameBytes)
        {
            return Result.FromFailure($"Frame of {frame.Length} bytes exceeds the {_maxFrameBytes} byte cap.");
        }

        //  Enqueue for the dedicated send thread. Sends never block the calling thread. Reliable control/state
        //  messages ride a never-evicting priority queue (a drop would be permanent data loss); per-tick
        //  snapshot traffic rides the bounded queue where a full queue drops the oldest frame so input
        //  staleness is bounded instead of the queue growing without limit.
        if (SendPriority.IsReliable(typeof(T)))
        {
            EnqueueReliable(bytes);
            return Result.FromSuccess();
        }

        if (_sendQueue.TryAdd(bytes))
        {
            return Result.FromSuccess();
        }

        _logger.LogWarning("Per-tick send queue full; dropping the oldest frame.");
        _sendQueue.TryTake(out _);
        if (_sendQueue.TryAdd(bytes))
        {
            return Result.FromSuccess();
        }

        return Result.FromFailure("Send queue is full.");
    }

    /// <summary>
    /// Enqueues a never-evicting reliable frame. The queue is unbounded by design (drops are data loss),
    /// so a peer that stops reading surfaces as a grow-and-error condition instead: once the concern
    /// threshold is crossed, an error is logged and re-logged roughly every 100 enqueues while it stays
    /// over the threshold, and a peer holding the backlog past the disconnect threshold for the
    /// disconnect window is marked broken (the host drops it, bounding per-peer memory).
    /// </summary>
    private void EnqueueReliable(byte[] bytes)
    {
        _reliableQueue.Add(bytes);

        int count = _reliableQueue.Count;
        if (count > _reliableConcernThreshold && count - _reliableConcernLoggedCount >= 100)
        {
            _reliableConcernLoggedCount = count;
            _logger.LogError("Reliable send queue holds {count} frames, over the {threshold} concern threshold; a peer is not reading.", count, _reliableConcernThreshold);
        }

        if (count > _reliableDisconnectThreshold)
        {
            if (_reliableOverflowTicks == 0)
            {
                _reliableOverflowTicks = Environment.TickCount;
            }

            if (Environment.TickCount - _reliableOverflowTicks >= _reliableDisconnectMs)
            {
                _logger.LogError("Dropping peer: reliable backlog of {count} frames held over {ms} ms.", count, _reliableDisconnectMs);
                MarkBroken();
            }
        }
        else
        {
            _reliableOverflowTicks = 0;
        }
    }

    public Result<T> Receive<T>()
    {
        if (!_serializers.TryGet<T>(out ISerializer<T> serializer))
        {
            return Result<T>.FromFailure($"No serializer registered for type {typeof(T).Name}.");
        }

        if (!_receiveQueues.TryGetValue(typeof(T), out ConcurrentQueue<byte[]>? queue) || !queue.TryDequeue(out byte[]? data))
        {
            return Result<T>.FromFailure("No messages available.");
        }

        try
        {
            return Result<T>.FromSuccess(serializer.Deserialize(data));
        }
        catch (Exception ex)
        {
            return Result<T>.FromFailure(ex);
        }
    }

    public void Dispose()
    {
        Disconnect();

        //  Cancel wakes each loop, but a not-yet-scheduled thread can still read the cancellation token
        //  after we dispose its source, throwing ObjectDisposedException on a background thread (which
        //  crashes the process). The CTSs and the queue are therefore left for GC: cancellation makes
        //  every loop exit promptly, and the joined threads cannot outlive the transport.
        JoinIfAlive(_receiveThread);
        JoinIfAlive(_sendThread);
        JoinIfAlive(_keepaliveThread);
    }

    private void StartLoops()
    {
        _isRunning = true;
        _receiveThread = new Thread(ReceiveLoop)
        {
            IsBackground = true,
            Name = "TcpTransport Receive"
        };
        _receiveThread.Start();

        _sendThread = new Thread(SendLoop)
        {
            IsBackground = true,
            Name = "TcpTransport Send"
        };
        _sendThread.Start();

        //  Keepalive keeps a live-but-idle link from tripping the socket read timeout on either peer. Both
        //  ends run the loop, so both receive directions always see a byte within the timeout window.
        _keepaliveThread = new Thread(KeepaliveLoop)
        {
            IsBackground = true,
            Name = "TcpTransport Keepalive"
        };
        _keepaliveThread.Start();
    }

    private void KeepaliveLoop()
    {
        WaitHandle wait = _keepaliveCts.Token.WaitHandle;

        while (true)
        {
            if (_keepaliveCts.IsCancellationRequested)
            {
                break;
            }

            wait.WaitOne(_keepaliveIntervalMs);

            if (_keepaliveCts.IsCancellationRequested || !_isRunning)
            {
                break;
            }

            //  Enqueue for the send thread so it stays the single writer to the socket. A full queue
            //  means real traffic already keeps the link warm, so dropping the keepalive is fine.
            if (_sendQueue.TryAdd(_KEEPALIVE_FRAME))
            {
                continue;
            }

            _sendQueue.TryTake(out _);
            _sendQueue.TryAdd(_KEEPALIVE_FRAME);
        }
    }

    private void SendLoop()
    {
        while (_isRunning)
        {
            byte[] bytes;
            try
            {
                //  Reliable frames drain first (never dropped), then per-tick frames. TryTake on the
                //  unbounded reliable queue is non-blocking; the per-tick Take blocks until a frame or
                //  a cancel, so the loop parks on the snapshot queue when both are empty.
                if (_reliableQueue.TryTake(out byte[]? reliable))
                {
                    bytes = reliable;
                }
                else
                {
                    bytes = _sendQueue.Take(_sendCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                break; //  Intentional Disconnect.
            }

            if (!_isRunning)
            {
                break;
            }

            try
            {
                _stream?.Write(bytes, 0, bytes.Length);
            }
            catch (Exception)
            {
                //  A write timeout or socket failure means the peer is gone. Surface it exactly once.
                MarkBroken();
                break;
            }
        }
    }

    private void ReceiveLoop()
    {
        byte[] lengthBuffer = new byte[4];

        while (_isRunning)
        {
            try
            {
                if (ReadExact(lengthBuffer, 0, 4) == 0)
                {
                    break;
                }

                int frameLength = BitConverter.ToInt32(lengthBuffer, 0);
                if (frameLength < 4 || frameLength > _maxFrameBytes)
                {
                    //  A peer claiming a size beyond the cap cannot be satisfied without allocating its
                    //  buffer; treat the frame as a protocol violation and drop the connection.
                    _logger.LogWarning("Dropping connection: frame length {frameLength} exceeds the {maxFrameBytes} byte cap.", frameLength, _maxFrameBytes);
                    break;
                }

                byte[] frame = new byte[frameLength];
                if (ReadExact(frame, 0, frameLength) == 0)
                {
                    break;
                }

                int typeTagLength = BitConverter.ToInt32(frame, 0);
                if (typeTagLength < 0 || 4 + typeTagLength > frameLength)
                {
                    break;
                }

                if (typeTagLength == 0)
                {
                    continue; //  Keepalive heartbeat, no message body.
                }

                string typeName = Encoding.UTF8.GetString(frame, 4, typeTagLength);
                byte[] payload = new byte[frameLength - 4 - typeTagLength];
                Array.Copy(frame, 4 + typeTagLength, payload, 0, payload.Length);

                if (!_serializers.TryGetType(typeName, out Type type))
                {
                    _logger.LogWarning("Dropping frame with unknown type tag '{typeName}'.", typeName);
                    continue;
                }

                _receiveQueues.GetOrAdd(type, static _ => new ConcurrentQueue<byte[]>()).Enqueue(payload);
            }
            catch
            {
                break;
            }
        }

        //  If we exited the loop due to a peer disconnect (not an intentional Disconnect, which already
        //  cleared _isRunning), surface the disconnect so a host can drop the client from its hub and a
        //  client can return to the menu.
        if (_isRunning)
        {
            MarkBroken();
        }
    }

    /// <summary>
    /// Marks the transport broken after a peer disconnect detected by either the receive or the send
    /// thread, canceling the send drain and raising <see cref="OnDisconnected"/> exactly once.
    /// </summary>
    private void MarkBroken()
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;
        _sendCts.Cancel();
        _keepaliveCts.Cancel();

        if (_disconnectedRaised)
        {
            return;
        }

        _disconnectedRaised = true;
        try
        {
            OnDisconnected?.Invoke();
        }
        catch
        {
            //  A subscriber's exception must not kill the detecting thread.
        }
    }

    private static int ClampKeepaliveInterval(int keepaliveIntervalMs, int connectionTimeoutMs)
    {
        int clamped = keepaliveIntervalMs > 0 ? keepaliveIntervalMs : 2000;
        int ceiling = Math.Max(1, connectionTimeoutMs / 2);
        return Math.Max(250, Math.Min(clamped, ceiling));
    }

    private static void JoinIfAlive(Thread? thread)
    {
        //  The disconnecting owner may be invoked from the receive thread itself (MarkBroken raises
        //  OnDisconnected on it); a thread cannot join itself.
        if (thread != null && thread != Thread.CurrentThread && thread.IsAlive)
        {
            thread.Join(_THREAD_JOIN_TIMEOUT_MS);
        }
    }

    private int ReadExact(byte[] buffer, int offset, int count)
    {
        int totalRead = 0;

        while (totalRead < count)
        {
            int read = _stream!.Read(buffer, offset + totalRead, count - totalRead);

            if (read == 0)
            {
                return totalRead;
            }

            totalRead += read;
        }

        return totalRead;
    }
}