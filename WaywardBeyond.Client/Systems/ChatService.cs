using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using WaywardBeyond.Config;
using WaywardBeyond.Networking;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// A received chat message paired with the monotonic timestamp at which the client received it. The
/// timestamp drives the per-message fade in the chat overlay, so it is monotonic across renders.
/// </summary>
public readonly record struct ChatLine(ChatMessage Message, long ReceivedAt);

/// <summary>
/// Client-side chat state shared between the ECS thread (<see cref="ClientChatSystem"/>) and the UI thread
/// (the chat layer): a bounded ring buffer of received messages for scrollback and a queue of outbound
/// messages waiting for the transport. The buffer capacity comes from <see cref="ChatConfig.MaxHistory"/>.
/// </summary>
public sealed class ChatService(in ChatConfig config)
{
    private readonly Lock _sync = new();
    private readonly ChatLine[] _messages = new ChatLine[Math.Max(1, config.MaxHistory.Get())];
    private readonly ConcurrentQueue<string> _pendingSends = new();
    private int _head;
    private int _count;

    public int Capacity => _messages.Length;

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _count;
            }
        }
    }

    /// <summary>Appends a received message with its arrival timestamp, evicting the oldest when the buffer is full.</summary>
    public void Add(in ChatMessage message)
    {
        var line = new ChatLine(message, Stopwatch.GetTimestamp());
        lock (_sync)
        {
            if (_count == _messages.Length)
            {
                _messages[_head] = line;
                _head = (_head + 1) % _messages.Length;
            }
            else
            {
                _messages[(_head + _count) % _messages.Length] = line;
                _count++;
            }
        }
    }

    /// <summary>Copies all buffered messages in chronological order (oldest first).</summary>
    public ChatLine[] Snapshot()
    {
        lock (_sync)
        {
            var snapshot = new ChatLine[_count];
            for (var i = 0; i < _count; i++)
            {
                snapshot[i] = _messages[(_head + i) % _messages.Length];
            }

            return snapshot;
        }
    }

    public void EnqueueSend(string value)
    {
        _pendingSends.Enqueue(value);
    }

    public bool TryDequeueSend(out string value)
    {
        return _pendingSends.TryDequeue(out value!);
    }
}