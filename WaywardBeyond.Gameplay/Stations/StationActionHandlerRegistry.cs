using System;
using System.Collections.Generic;
using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Gameplay.Stations;

/// <inheritdoc/>
public sealed class StationActionHandlerRegistry : IStationActionHandlerRegistry
{
    private const int PRIORITY_COUNT = 3;

    private readonly IReadOnlyList<IStationActionHandler> _handlers;

    /// <summary>Builds the ordered handler list from DI registrations, grouped by priority and registration order.</summary>
    public StationActionHandlerRegistry(IEnumerable<IStationActionHandler> handlers)
    {
        if (handlers == null)
        {
            throw new ArgumentNullException(nameof(handlers));
        }

        var buckets = new List<IStationActionHandler>[PRIORITY_COUNT];
        for (var i = 0; i < buckets.Length; i++)
        {
            buckets[i] = [];
        }

        foreach (IStationActionHandler handler in handlers)
        {
            if (handler == null)
            {
                throw new ArgumentException("A station action handler must not be null.", nameof(handlers));
            }

            if (!Enum.IsDefined(handler.Priority))
            {
                throw new ArgumentOutOfRangeException(nameof(handlers), handler.Priority, "The station action handler priority is not defined.");
            }

            buckets[(int)handler.Priority].Add(handler);
        }

        var ordered = new List<IStationActionHandler>();
        for (var p = 0; p < PRIORITY_COUNT; p++)
        {
            ordered.AddRange(buckets[p]);
        }

        _handlers = Array.AsReadOnly(ordered.ToArray());
    }

    /// <inheritdoc/>
    public StationActionResult Dispatch(in StationActionContext context)
    {
        for (var i = 0; i < _handlers.Count; i++)
        {
            IStationActionHandler handler = _handlers[i];
            if (!Matches(handler.Filter, in context))
            {
                continue;
            }

            StationActionResult result = handler.Handle(in context);
            if (result.Decision != StationActionDecision.Continue)
            {
                return result;
            }
        }

        return StationActionResult.Reject("Unhandled station action.");
    }

    /// <inheritdoc/>
    public IReadOnlyList<IStationActionHandler> GetHandlers()
    {
        return _handlers;
    }

    private static bool Matches(in StationActionFilter filter, in StationActionContext context)
    {
        if (filter.Action != null && filter.Action.Value != context.Op.Action)
        {
            return false;
        }

        if (filter.ActionId != null && !string.Equals(filter.ActionId, context.Op.ActionId, StringComparison.Ordinal))
        {
            return false;
        }

        if (filter.GameMode != null && filter.GameMode.Value != context.GameMode)
        {
            return false;
        }

        return true;
    }
}
