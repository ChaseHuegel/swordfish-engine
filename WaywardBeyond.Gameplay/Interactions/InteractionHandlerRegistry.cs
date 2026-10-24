using System;
using System.Collections.Generic;

namespace WaywardBeyond.Gameplay.Interactions;

/// <inheritdoc/>
public sealed class InteractionHandlerRegistry : IInteractionHandlerRegistry
{
    private const int STAGE_COUNT = 5;
    private const int PRIORITY_COUNT = 3;

    //  One ordered list per stage: handlers are placed by their own stage and priority when the registry
    //  is built, so a run is a flat walk with no per-interaction sorting or allocation. The lists are
    //  read-only wrappers, so a caller cannot cast them back to an array and mutate the pipeline.
    private readonly IReadOnlyList<IInteractionHandler>[] _stages = new IReadOnlyList<IInteractionHandler>[STAGE_COUNT];

    /// <summary>Builds the pipeline from the registered handlers, ordered by stage and priority.</summary>
    public InteractionHandlerRegistry(IEnumerable<IInteractionHandler> handlers)
    {
        if (handlers == null)
        {
            throw new ArgumentNullException(nameof(handlers));
        }

        var buckets = new List<IInteractionHandler>[STAGE_COUNT * PRIORITY_COUNT];
        for (var index = 0; index < buckets.Length; index++)
        {
            buckets[index] = [];
        }

        foreach (IInteractionHandler handler in handlers)
        {
            if (handler == null)
            {
                throw new ArgumentException("An interaction handler must not be null.", nameof(handlers));
            }

            RequireDefined(handler.Stage, nameof(handler.Stage));
            RequireDefined(handler.Priority, nameof(handler.Priority));
            buckets[Index(handler.Stage, handler.Priority)].Add(handler);
        }

        for (var stage = 0; stage < STAGE_COUNT; stage++)
        {
            var ordered = new List<IInteractionHandler>();
            for (var priority = 0; priority < PRIORITY_COUNT; priority++)
            {
                ordered.AddRange(buckets[(stage * PRIORITY_COUNT) + priority]);
            }

            _stages[stage] = Array.AsReadOnly(ordered.ToArray());
        }
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

    /// <inheritdoc/>
    public IReadOnlyList<IInteractionHandler> GetHandlers(InteractionStage stage)
    {
        RequireDefined(stage, nameof(stage));
        return _stages[(int)stage];
    }

    private InteractionDecision RunStage(InteractionStage stage, ref InteractionContext context)
    {
        IReadOnlyList<IInteractionHandler> handlers = _stages[(int)stage];
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

    private static void RequireDefined<TEnum>(TEnum value, string parameterName) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The interaction stage or priority is not defined.");
        }
    }

    private static int Index(InteractionStage stage, InteractionPriority priority)
    {
        return ((int)stage * PRIORITY_COUNT) + (int)priority;
    }
}
