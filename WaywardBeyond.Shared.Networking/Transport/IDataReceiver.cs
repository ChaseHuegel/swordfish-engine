using System;
using WaywardBeyond.Shared.Networking.Events;

namespace WaywardBeyond.Shared.Networking.Transport;

public interface IDataReceiver
{
    event EventHandler<DataEventArgs>? Received;
}
