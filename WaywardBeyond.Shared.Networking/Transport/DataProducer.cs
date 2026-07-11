using System;
using System.Collections.Generic;
using WaywardBeyond.Shared.Networking.Events;

namespace WaywardBeyond.Shared.Networking.Transport;

public class DataProducer : IDataProducer, IDisposable
{
    private IDataReceiver[]? _dataServices;
    private readonly IParser _parser;
    private bool _disposed;

    public event EventHandler<DataEventArgs>? Received;

    public DataProducer(IDataReceiver[] dataServices, IParser parser)
    {
        _dataServices = dataServices;
        _parser = parser;
        for (int i = 0; i < dataServices.Length; i++)
        {
            IDataReceiver dataService = dataServices[i];
            dataService.Received += OnDataReceived;
        }
    }

    private void OnDataReceived(object? sender, DataEventArgs e)
    {
        List<byte[]> dataPackets = _parser.Parse(e.Data);
        for (int i = 0; i < dataPackets.Count; i++)
        {
            byte[] packet = dataPackets[i];
            Received?.Invoke(this, new DataEventArgs(packet, e.Sender));
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing && _dataServices != null)
        {
            for (int i = 0; i < _dataServices.Length; i++)
            {
                IDataReceiver dataService = _dataServices[i];
                dataService.Received -= OnDataReceived;
            }
            _dataServices = null;
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~DataProducer()
    {
        Dispose(false);
    }
}
