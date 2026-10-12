using System.Collections.Generic;

namespace WaywardBeyond.Gameplay.Interactions;

/// <inheritdoc/>
public sealed class InteractionHandlerRegistry : IInteractionHandlerRegistry
{
    private readonly List<IInteractionHandler> _handlers = [];

    /// <inheritdoc/>
    public void Register(IInteractionHandler handler)
    {
        _handlers.Add(handler);
    }

    /// <inheritdoc/>
    public InteractionResolution Apply(in InteractionRequest request, in InteractionResolution baseResolution, string? heldItemID)
    {
        InteractionContext context = new(request, baseResolution, heldItemID);
        InteractionResolution resolution = baseResolution;

        foreach (IInteractionHandler handler in _handlers)
        {
            if (!Matches(handler.Filter, context))
            {
                continue;
            }

            resolution = handler.Handle(context);
            if (resolution.Action == InteractionAction.None)
            {
                return resolution;
            }
        }

        return resolution;
    }

    private static bool Matches(in InteractionHandlerFilter filter, in InteractionContext context)
    {
        if (filter.Kind != null && filter.Kind.Value != context.Request.Kind)
        {
            return false;
        }

        if (filter.HeldItemID != null && filter.HeldItemID != context.HeldItemID)
        {
            return false;
        }

        if (filter.GameMode != null && filter.GameMode.Value != context.Request.GameMode)
        {
            return false;
        }

        return true;
    }
}