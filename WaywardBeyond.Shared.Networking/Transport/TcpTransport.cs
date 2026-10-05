using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Util;
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
    private readonly ConcurrentDictionary<Type, ConcurrentQueue<QueuedFrame>> _receiveQueues = new();
    private readonly BlockingCollection<QueuedFrame> _sendQueue;
    private readonly BlockingCollection<QueuedFrame> _reliableQueue = new();
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
        _sendQueue = new BlockingCollection<QueuedFrame>(_sendQueueSize);
    }

    /// <summary>
    /// A pooled buffer queued for the send loop or a receive dispatch. The queue owns the buffer; the
    /// consumer returns it to the shared pool after use, so the hot path allocates nothing per frame.
    /// </summary>
    private readonly struct QueuedFrame(byte[] buffer, int length, bool pooled)
    {
        public readonly byte[] Buffer = buffer;
        public readonly int Length = length;
        private readonly bool _pooled = pooled;

        public void Return()
        {
            //  Only pooled rentals return; exact-size dispatch buffers are plain arrays.
            if (_pooled)
            {
                ArrayPool<byte>.Shared.Return(Buffer);
            }
        }
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
        byte[]? typeTag = _serializers.TryGetTypeTag<T>();
        if (typeTag == null)
        {
            return Result.FromFailure($"No serializer registered for type {typeof(T).Name}.");
        }

        byte[] payload = serializer.Serialize(message);

        //  Frame body = [4-byte body length][4-byte type-tag length][type tag][payload], built in one
        //  pooled buffer. The queue owns the buffer; the send loop returns it to the pool after writing.
        int frameLength = 4 + typeTag.Length + payload.Length;

        //  Refuse frames over the negotiated cap so the receive side never sees a prefix it must drop a
        //  peer for. Serialization already happened; we only skip the queue.
        if (frameLength > _maxFrameBytes)
        {
            return Result.FromFailure($"Frame of {frameLength} bytes exceeds the {_maxFrameBytes} byte cap.");
        }

        int totalLength = 4 + frameLength;
        byte[] bytes = ArrayPool<byte>.Shared.Rent(totalLength);
        int offset = 0;
        WriteInt(bytes, offset, frameLength);
        offset += 4;
        WriteInt(bytes, offset, typeTag.Length);
        offset += 4;
        typeTag.CopyTo(bytes, offset);
        offset += typeTag.Length;
        payload.CopyTo(bytes, offset);

        if (_traceLogging)
        {
            _logger.LogTrace("Sending {type} frame ({bytes} bytes).", typeof(T).Name, totalLength);
        }

        var queued = new QueuedFrame(bytes, totalLength, pooled: true);

        //  Enqueue for the dedicated send thread. Sends never block the calling thread. Reliable control/state
        //  messages ride a never-evicting priority queue (a drop would be permanent data loss); per-tick
        //  snapshot traffic rides the bounded queue where a full queue drops the oldest frame so input
        //  staleness is bounded instead of the queue growing without limit.
        if (SendPriority.IsReliable(typeof(T)))
        {
            EnqueueReliable(queued);
            return Result.FromSuccess();
        }

        if (_sendQueue.TryAdd(queued))
        {
            return Result.FromSuccess();
        }

        _logger.LogWarning("Per-tick send queue full; dropping the oldest frame.");
        if (_sendQueue.TryTake(out QueuedFrame dropped))
        {
            dropped.Return();
        }
        if (_sendQueue.TryAdd(queued))
        {
            return Result.FromSuccess();
        }

        queued.Return();
        return Result.FromFailure("Send queue is full.");
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), value);
    }

    /// <summary>
    /// Enqueues a never-evicting reliable frame. The queue is unbounded by design (drops are data loss),
    /// so a peer that stops reading surfaces as a grow-and-error condition instead: once the concern
    /// threshold is crossed, an error is logged and re-logged roughly every 100 enqueues while it stays
    /// over the threshold, and a peer holding the backlog past the disconnect threshold for the
    /// disconnect window is marked broken (the host drops it, bounding per-peer memory).
    /// </summary>
    private void EnqueueReliable(QueuedFrame frame)
    {
        _reliableQueue.Add(frame);

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

    public Result SendRaw(in byte[] frame)
    {
        if (!_isRunning)
        {
            return Result.FromFailure("Transport is not running.");
        }

        if (_sendQueue.TryAdd(new QueuedFrame(frame, frame.Length, pooled: false)))
        {
            return Result.FromSuccess();
        }

        _logger.LogWarning("Per-tick send queue full; dropping the oldest frame.");
        if (_sendQueue.TryTake(out QueuedFrame dropped))
        {
            dropped.Return();
        }

        return _sendQueue.TryAdd(new QueuedFrame(frame, frame.Length, pooled: false))
            ? Result.FromSuccess()
            : Result.FromFailure("Send queue is full.");
    }

    public Result<T> Receive<T>()
    {
        if (!_serializers.TryGet<T>(out ISerializer<T> serializer))
        {
            return Result<T>.FromFailure($"No serializer registered for type {typeof(T).Name}.");
        }

        if (!_receiveQueues.TryGetValue(typeof(T), out ConcurrentQueue<QueuedFrame>? queue) || !queue.TryDequeue(out QueuedFrame frame))
        {
            return Result<T>.FromFailure("No messages available.");
        }

        try
        {
            return Result<T>.FromSuccess(serializer.Deserialize(frame.Buffer));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to decode a {type} frame ({bytes} bytes): {message}.", typeof(T).Name, frame.Length, ex.Message);
            return Result<T>.FromFailure(ex);
        }
        finally
        {
            //  The deserializer copies into its own buffers; the dispatch buffer returns to the pool.
            frame.Return();
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
        var pendingFrames = new List<QueuedFrame>(32);
        WaitHandle wake = _sendCts.Token.WaitHandle;

        while (_isRunning)
        {
            pendingFrames.Clear();

            //  Reliable frames drain first (never dropped), then per-tick frames. Poll both queues
            //  non-blocking (a reliable frame arriving must wake the drain even when the per-tick queue
            //  is empty), and park on a timed wait when nothing is pending: at most one send interval
            //  elapses between drains, so every pending frame coalesces into one socket write.
            if (!_reliableQueue.TryTake(out QueuedFrame reliable) && !_sendQueue.TryTake(out reliable))
            {
                wake.WaitOne(_sendIntervalMs);
                continue;
            }

            pendingFrames.Add(reliable);

            //  Coalesce everything else that arrived this interval (reliable first, then per-tick).
            while (_reliableQueue.TryTake(out QueuedFrame moreReliable))
            {
                pendingFrames.Add(moreReliable);
            }
            while (_sendQueue.TryTake(out QueuedFrame perTick))
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

    private void WriteAll(List<QueuedFrame> frames)
    {
        //  One socket write per interval: concatenate the pending frames so a burst (snapshots,
        //  world stream) goes out as far fewer, larger segments instead of 60 tiny writes per second.
        //  The combined segment is pooled too; every queued frame rental returns to the pool here.
        int total = 0;
        for (var i = 0; i < frames.Count; i++)
        {
            total += frames[i].Length;
        }

        byte[] combined = ArrayPool<byte>.Shared.Rent(total);
        int offset = 0;
        for (var i = 0; i < frames.Count; i++)
        {
            frames[i].Buffer.AsSpan(0, frames[i].Length).CopyTo(combined.AsSpan(offset));
            offset += frames[i].Length;
            frames[i].Return();
        }

        try
        {
            _stream?.Write(combined, 0, total);
            Interlocked.Add(ref _packetsSent, frames.Count);
            Interlocked.Add(ref _bytesSent, total);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(combined);
        }
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

                byte[] frame = ArrayPool<byte>.Shared.Rent(frameLength);
                int totalRead = ReadExact(frame, 0, frameLength);
                if (totalRead == 0)
                {
                    ArrayPool<byte>.Shared.Return(frame);
                    reason = DisconnectReason.PeerClosed;
                    break;
                }

                int typeTagLength = BitConverter.ToInt32(frame, 0);
                if (typeTagLength < 0 || 4 + typeTagLength > frameLength)
                {
                    ArrayPool<byte>.Shared.Return(frame);
                    reason = DisconnectReason.ReadError;
                    break;
                }

                string typeName = Encoding.UTF8.GetString(frame, 4, typeTagLength);

                if (!_serializers.TryGetType(typeName, out Type type))
                {
                    ArrayPool<byte>.Shared.Return(frame);
                    _logger.LogWarning("Dropping frame with unknown type tag '{typeName}' ({bytes} bytes).", typeName, frameLength - 4 - typeTagLength);
                    continue;
                }

                //  The per-type dispatch queue owns the exact-size payload (the deserializer walks the full array
                //  length, so pooled oversized buffers cannot be handed out); the frame rental returns now.
                int payloadLength = frameLength - 4 - typeTagLength;
                byte[] payload = new byte[payloadLength];
                Array.Copy(frame, 4 + typeTagLength, payload, 0, payloadLength);
                ArrayPool<byte>.Shared.Return(frame);

                if (_traceLogging)
                {
                    _logger.LogTrace("Received {type} frame ({bytes} bytes).", typeName, payloadLength);
                }

                Interlocked.Increment(ref _packetsReceived);
                Interlocked.Add(ref _bytesReceived, 4 + frameLength);
                _receiveQueues.GetOrAdd(type, static _ => new ConcurrentQueue<QueuedFrame>()).Enqueue(new QueuedFrame(payload, payloadLength, pooled: false));
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