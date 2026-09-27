using System;
using System.Collections.Concurrent;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Networking;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Client-side chat state shared between the ECS thread (<see cref="ClientChatSystem"/>) and the UI thread
/// (the chat layer): a bounded ring buffer of received messages for scrollback and a queue of outbound
/// messages waiting for the transport. The buffer capacity comes from <see cref="ChatSettings.MaxHistory"/>.
/// </summary>
public sealed class ChatService
{
    private readonly object _sync = new();
    private readonly ChatMessage[] _messages;
    private readonly ConcurrentQueue<string> _pendingSends = new();
    private int _head;
    private int _count;

    public ChatService(ChatSettings settings)
    {
        _messages = new ChatMessage[Math.Max(1, settings.MaxHistory.Get())];
    }

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

    /// <summary>Appends a received message, evicting the oldest when the buffer is full.</summary>
    public void Add(in ChatMessage message)
    {
        lock (_sync)
        {
            if (_count == _messages.Length)
            {
                _messages[_head] = message;
                _head = (_head + 1) % _messages.Length;
            }
            else
            {
                _messages[(_head + _count) % _messages.Length] = message;
                _count++;
            }
        }
    }

    /// <summary>Copies all buffered messages in chronological order (oldest first).</summary>
    public ChatMessage[] Snapshot()
    {
        lock (_sync)
        {
            var snapshot = new ChatMessage[_count];
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