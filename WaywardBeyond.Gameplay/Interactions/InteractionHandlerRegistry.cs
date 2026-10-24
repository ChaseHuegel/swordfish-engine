using System;
using System.Collections.Generic;

namespace WaywardBeyond.Gameplay.Interactions;

/// <inheritdoc/>
public sealed class InteractionHandlerRegistry : IInteractionHandlerRegistry
{
    private const int STAGE_COUNT = 5;
    private const int PRIORITY_COUNT = 3;

    //  One list per (stage, priority). Registration happens at startup, so the nested walk a run does
    //  is over the pre-sorted bands: no per-interaction sorting or allocation.
    private readonly List<IInteractionHandler>[] _handlers = new List<IInteractionHandler>[STAGE_COUNT * PRIORITY_COUNT];

    /// <summary>Creates an empty registry.</summary>
    public InteractionHandlerRegistry()
    {
        for (var index = 0; index < _handlers.Length; index++)
        {
            _handlers[index] = [];
        }
    }

    /// <inheritdoc/>
    public void Register(InteractionStage stage, InteractionPriority priority, IInteractionHandler handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _handlers[Index(stage, priority)].Add(handler);
    }

    /// <inheritdoc/>
    public InteractionRunResult Run(ref InteractionContext context)
    {
        for (var stage = 0; stage < STAGE_COUNT; stage++)
        {
            InteractionDecision decision = RunStage((InteractionStage)stage, ref context);
            if (decision is InteractionDecision.Consume or InteractionDecision.Cancel)
            {
                return new InteractionRunResult(in context.Resolution, decision);
            }
        }

        return new InteractionRunResult(in context.Resolution, InteractionDecision.Continue);
    }

    private InteractionDecision RunStage(InteractionStage stage, ref InteractionContext context)
    {
        for (var priority = 0; priority < PRIORITY_COUNT; priority++)
        {
            List<IInteractionHandler> handlers = _handlers[Index(stage, (InteractionPriority)priority)];
            for (var index = 0; index < handlers.Count; index++)
            {
                IInteractionHandler handler = handlers[index];
                if (!Matches(handler.Filter, in context))
                {
                    continue;
                }

                InteractionDecision decision = handler.Handle(ref context);
                if (decision != InteractionDecision.Continue)
                {
                    //  Handled ends this stage; Consume and Cancel end the whole pipeline.
                    return decision;
                }
            }
        }

        return InteractionDecision.Continue;
    }

    private static bool Matches(in InteractionHandlerFilter filter, in InteractionContext context)
    {
        if (filter.Kind != null && filter.Kind.Value != context.Kind)
        {
            return false;
        }

        if (filter.HeldItemID != null && filter.HeldItemID != context.HeldItemID)
        {
            return false;
        }

        if (filter.GameMode != null && filter.GameMode.Value != context.GameMode)
        {
            return false;
        }

        return true;
    }

    private static int Index(InteractionStage stage, InteractionPriority priority)
    {
        return (int)stage * PRIORITY_COUNT + (int)priority;
    }
}
