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
    private readonly bool _traceLogging;
    private readonly int _connectionTimeoutMs;
    private readonly int _sendQueueSize;
    private readonly int _maxFrameBytes;
    private readonly int _sendIntervalMs;
    private readonly ConcurrentDictionary<Type, ConcurrentQueue<byte[]>> _receiveQueues = new();
    private readonly BlockingCollection<byte[]> _sendQueue;
    private readonly BlockingCollection<byte[]> _reliableQueue = new();
    private readonly int _reliableConcernThreshold;
    private readonly int _reliableDisconnectThreshold;
    private readonly int _reliableDisconnectMs;
    private int _reliableConcernLoggedCount;
    private int _reliableOverflowTicks;
    private readonly CancellationTokenSource _sendCts = new();

    private long _packetsSent;
    private long _packetsReceived;
    private long _bytesSent;
    private long _bytesReceived;

    public long PacketsSent => Interlocked.Read(ref _packetsSent);
    public long PacketsReceived => Interlocked.Read(ref _packetsReceived);
    public long BytesSent => Interlocked.Read(ref _bytesSent);
    public long BytesReceived => Interlocked.Read(ref _bytesReceived);
    private TcpClient? _client;
    private TcpListener? _listener;
    private NetworkStream? _stream;
    private volatile bool _isRunning;
    private volatile bool _disconnectedRaised;
    private Thread? _receiveThread;
    private Thread? _sendThread;


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
    /// is still running — i.e. not on an intentional <see cref="Disconnect"/>, which never raises it. The
    /// reason discriminates EOF, read/write errors, timeouts, and the backlog-limit drop. Used by the
    /// server host to drop a departed client from its connection hub, and by the owner (LanHost,
    /// TransportManager) to dispose the transport exactly once.
    /// </summary>
    public event Action<DisconnectReason>? OnDisconnected;

    public TcpTransport(
        IEnumerable<INetworkSerializer> serializers,
        ILoggerFactory? loggerFactory = null,
        int connectionTimeoutMs = 5000,
        int sendQueueSize = 256,
        int maxFrameBytes = 16 * 1024 * 1024,
        int reliableQueueConcernThreshold = 64,
        int reliableQueueDisconnectThreshold = 128,
        int reliableQueueDisconnectMs = 10_000,
        bool traceLogging = false,
        int sendIntervalMs = 16
    ) {
        _serializers = new SerializerCache(serializers);
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<TcpTransport>();
        _traceLogging = traceLogging;
        _connectionTimeoutMs = connectionTimeoutMs;
        _sendQueueSize = Math.Max(1, sendQueueSize);
        _maxFrameBytes = Math.Max(64, maxFrameBytes);
        _reliableConcernThreshold = Math.Max(1, reliableQueueConcernThreshold);
        _reliableDisconnectThreshold = Math.Max(_reliableConcernThreshold, reliableQueueDisconnectThreshold);
        _reliableDisconnectMs = Math.Max(1, reliableQueueDisconnectMs);
        _sendIntervalMs = Math.Max(1, sendIntervalMs);
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
        int maxFrameBytes = 16 * 1024 * 1024,
        int reliableQueueConcernThreshold = 64,
        int reliableQueueDisconnectThreshold = 128,
        int reliableQueueDisconnectMs = 10_000,
        bool traceLogging = false,
        int sendIntervalMs = 16
    ) {
        var transport = new TcpTransport(serializers, loggerFactory, connectionTimeoutMs, sendQueueSize, maxFrameBytes, reliableQueueConcernThreshold, reliableQueueDisconnectThreshold, reliableQueueDisconnectMs, traceLogging, sendIntervalMs);
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

        if (_traceLogging)
        {
            _logger.LogTrace("Sending {type} frame ({bytes} bytes).", typeName, bytes.Length);
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
                MarkBroken(DisconnectReason.BacklogLimit);
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
            _logger.LogWarning("Failed to decode a {type} frame ({bytes} bytes): {message}.", typeof(T).Name, data.Length, ex.Message);
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
    }


    private void SendLoop()
    {
        var pendingFrames = new List<byte[]>(32);
        WaitHandle wake = _sendCts.Token.WaitHandle;

        while (_isRunning)
        {
            pendingFrames.Clear();

            //  Reliable frames drain first (never dropped), then per-tick frames. Poll both queues
            //  non-blocking (a reliable frame arriving must wake the drain even when the per-tick queue
            //  is empty), and park on a timed wait when nothing is pending: at most one send interval
            //  elapses between drains, so every pending frame coalesces into one socket write.
            if (!_reliableQueue.TryTake(out byte[]? reliable) && !_sendQueue.TryTake(out reliable))
            {
                wake.WaitOne(_sendIntervalMs);
                continue;
            }

            pendingFrames.Add(reliable);

            //  Coalesce everything else that arrived this interval (reliable first, then per-tick).
            while (_reliableQueue.TryTake(out byte[]? moreReliable))
            {
                pendingFrames.Add(moreReliable);
            }
            while (_sendQueue.TryTake(out byte[]? perTick))
            {
                pendingFrames.Add(perTick);
            }

            try
            {
                WriteAll(pendingFrames);
            }
            catch (Exception ex)
            {
                //  A write timeout or socket failure means the peer is gone. Surface it exactly once.
                DisconnectReason reason = IsTimedOut(ex) ? DisconnectReason.WriteTimeout : DisconnectReason.WriteError;
                _logger.LogDebug("Peer write failed: {reason}.", reason);
                MarkBroken(reason);
                break;
            }
        }
    }

    private void WriteAll(List<byte[]> frames)
    {
        //  One socket write per interval: concatenate the pending frames so a burst (snapshots,
        //  world stream) goes out as far fewer, larger segments instead of 60 tiny writes per second.
        int total = 0;
        for (var i = 0; i < frames.Count; i++)
        {
            total += frames[i].Length;
        }

        var combined = new byte[total];
        int offset = 0;
        for (var i = 0; i < frames.Count; i++)
        {
            Array.Copy(frames[i], 0, combined, offset, frames[i].Length);
            offset += frames[i].Length;
        }

        _stream?.Write(combined, 0, combined.Length);
        Interlocked.Add(ref _packetsSent, frames.Count);
        Interlocked.Add(ref _bytesSent, combined.Length);
    }

    private void ReceiveLoop()
    {
        byte[] lengthBuffer = new byte[4];
        DisconnectReason reason = DisconnectReason.PeerClosed;

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
                    reason = DisconnectReason.ReadError;
                    break;
                }

                byte[] frame = new byte[frameLength];
                if (ReadExact(frame, 0, frameLength) == 0)
                {
                    reason = DisconnectReason.PeerClosed;
                    break;
                }

                int typeTagLength = BitConverter.ToInt32(frame, 0);
                if (typeTagLength < 0 || 4 + typeTagLength > frameLength)
                {
                    reason = DisconnectReason.ReadError;
                    break;
                }

                string typeName = Encoding.UTF8.GetString(frame, 4, typeTagLength);
                byte[] payload = new byte[frameLength - 4 - typeTagLength];
                Array.Copy(frame, 4 + typeTagLength, payload, 0, payload.Length);

                if (!_serializers.TryGetType(typeName, out Type type))
                {
                    _logger.LogWarning("Dropping frame with unknown type tag '{typeName}' ({bytes} bytes).", typeName, payload.Length);
                    continue;
                }

                if (_traceLogging)
                {
                    _logger.LogTrace("Received {type} frame ({bytes} bytes).", typeName, payload.Length);
                }

                Interlocked.Increment(ref _packetsReceived);
                Interlocked.Add(ref _bytesReceived, 4 + frameLength);
                _receiveQueues.GetOrAdd(type, static _ => new ConcurrentQueue<byte[]>()).Enqueue(payload);
            }
            catch (Exception ex)
            {
                reason = IsTimedOut(ex) ? DisconnectReason.ReadTimeout : DisconnectReason.ReadError;
                break;
            }
        }

        //  If we exited the loop due to a peer disconnect (not an intentional Disconnect, which already
        //  cleared _isRunning), surface the disconnect so a host can drop the client from its hub and a
        //  client can return to the menu.
        if (_isRunning)
        {
            MarkBroken(reason);
        }
    }

    /// <summary>
    /// Marks the transport broken after a peer disconnect detected by either the receive or the send
    /// thread, canceling the send drain and raising <see cref="OnDisconnected"/> exactly once with the
    /// detection <paramref name="reason"/>.
    /// </summary>
    private void MarkBroken(DisconnectReason reason)
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;
        _sendCts.Cancel();

        if (_disconnectedRaised)
        {
            return;
        }

        _disconnectedRaised = true;
        try
        {
            OnDisconnected?.Invoke(reason);
        }
        catch
        {
            //  A subscriber's exception must not kill the detecting thread.
        }
    }

    /// <summary>True when a socket exception chain indicates a timed-out read or write.</summary>
    private static bool IsTimedOut(Exception ex)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
        {
            if (current is SocketException { SocketErrorCode: SocketError.TimedOut })
            {
                return true;
            }
        }

        return false;
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