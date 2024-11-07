using Swordfish.Library.Util;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Events;

public interface IEventProcessor<in T>
{
    Result<EventBehavior> ProcessEvent(object sender, T e);
}