using System;
using WaywardBeyond.Shared.Networking.Events;

namespace WaywardBeyond.Shared.Networking.Messaging;

public interface IMessageConsumer<T>
{
    event EventHandler<MessageEventArgs<T>>? NewMessage;
}
