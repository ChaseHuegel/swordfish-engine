using Swordfish.Library.Events;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.Events;

internal class EventInvoker<TEvent>(in IEventProcessor<TEvent>[] processors)
{
    private readonly IEventProcessor<TEvent>[] _processors = processors;

    public Result Invoke(TEvent e)
    {
        for (var i = 0; i < _processors.Length; i++)
        {
            Result<EventBehavior> result = _processors[i].ProcessEvent(sender: this, e);
            if (!result.Success)
            {
                return Result.FromFailure("Event cancelled");
            }

            if (result.Value == EventBehavior.Consume)
            {
                return Result.FromSuccess();
            }
        }
        
        return Result.FromSuccess();
    }
}