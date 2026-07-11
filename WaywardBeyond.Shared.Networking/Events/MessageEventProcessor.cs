using System;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking.Messaging;

namespace WaywardBeyond.Shared.Networking.Events;

public sealed class MessageEventProcessor<TMessage>(
    ILogger logger,
    IMessageConsumer<TMessage> consumer,
    IEventProcessor<MessageEventArgs<TMessage>>[] processors
) : IMessageEventProcessor, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IMessageConsumer<TMessage> _consumer = consumer;
    private readonly IEventProcessor<MessageEventArgs<TMessage>>[] _processors = processors;

    public void Start()
    {
        _consumer.NewMessage += OnNewMessage;
    }

    public void Dispose()
    {
        _consumer.NewMessage -= OnNewMessage;
    }

    private void OnNewMessage(object? sender, MessageEventArgs<TMessage> e)
    {
        for (int i = 0; i < _processors.Length; i++)
        {
            IEventProcessor<MessageEventArgs<TMessage>> processor = _processors[i];
            Result<EventBehavior> result = processor.ProcessEvent(sender, e);
            if (!result)
            {
                _logger.LogWarning("{Processor} failed to process a {MessageType}: {Message}",
                    processor.GetType(), typeof(TMessage), result.Message);
            }
            if (result == EventBehavior.Consume)
            {
                break;
            }
        }
    }
}
