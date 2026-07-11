using Swordfish.Library.Collections.Filtering;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking.Sessions;

namespace WaywardBeyond.Shared.Networking.Messaging;

public interface IMessageProducer<T>
{
    Result Send(T message, Session target);
    Result Send(T message, IFilter<Session> targetFilter);
}
