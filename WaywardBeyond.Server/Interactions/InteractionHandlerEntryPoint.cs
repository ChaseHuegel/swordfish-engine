using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// Builds the interaction pipeline once the module set is complete and logs what it contains. The
/// registry resolves every <see cref="IInteractionHandler"/> from DI, so this entry point is the point
/// where a handler that a module registered late becomes part of the composition.
/// </summary>
public sealed class InteractionHandlerEntryPoint(
    IInteractionHandlerRegistry registry,
    ILogger<InteractionHandlerEntryPoint> logger
) : IEntryPoint
{
    /// <inheritdoc/>
    public void Run()
    {
        logger.LogInformation(
            "Loaded interaction handlers: select {select}, resolve {resolve}, validate {validate}, commit {commit}, react {react}.",
            registry.GetHandlers(InteractionStage.Select).Count,
            registry.GetHandlers(InteractionStage.Resolve).Count,
            registry.GetHandlers(InteractionStage.Validate).Count,
            registry.GetHandlers(InteractionStage.Apply).Count,
            registry.GetHandlers(InteractionStage.React).Count);
    }
}
