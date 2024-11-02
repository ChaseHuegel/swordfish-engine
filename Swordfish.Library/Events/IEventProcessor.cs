using Swordfish.Library.Util;

namespace Swordfish.Library.Events;

public interface IEventProcessor<in T>
{
    Result<EventBehavior> ProcessEvent(object sender, T e);
}