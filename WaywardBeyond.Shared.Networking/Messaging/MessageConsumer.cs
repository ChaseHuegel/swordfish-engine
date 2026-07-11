using System;
using System.Collections.Generic;
using Swordfish.Library.Serialization;
using WaywardBeyond.Shared.Networking.Events;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Shared.Networking.Messaging;

public class MessageConsumer<T> : IMessageConsumer<T>, IDisposable
{
    protected readonly ISerializer<T> _serializer;
    private IDataProducer[]? _dataProducers;
    private bool _disposed;

    public event EventHandler<MessageEventArgs<T>>? NewMessage;

    public MessageConsumer(ISerializer<T> serializer, IDataProducer[] dataProducers)
    {
        List<IDataProducer> matchingDataProducers = [];
        for (int i = 0; i < dataProducers.Length; i++)
        {
            IDataProducer dataProducer = dataProducers[i];
            matchingDataProducers.Add(dataProducer);
            dataProducer.Received += OnDataReceived;
        }
        _serializer = serializer;
        _dataProducers = [.. matchingDataProducers];
    }

    protected virtual void OnDataReceived(object? sender, DataEventArgs e)
    {
        T value = _serializer.Deserialize(e.Data);
        NewMessage?.Invoke(this, new MessageEventArgs<T>(value, e.Sender));
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing && _dataProducers != null)
        {
            for (int i = 0; i < _dataProducers.Length; i++)
            {
                IDataProducer dataProducer = _dataProducers[i];
                dataProducer.Received -= OnDataReceived;
            }
            _dataProducers = null;
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~MessageConsumer()
    {
        Dispose(false);
    }
}
