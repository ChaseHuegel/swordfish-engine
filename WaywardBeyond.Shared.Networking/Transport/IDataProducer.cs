using System;
using WaywardBeyond.Shared.Networking.Events;

namespace WaywardBeyond.Shared.Networking.Transport;

public interface IDataProducer
{
    event EventHandler<DataEventArgs>? Received;
}
