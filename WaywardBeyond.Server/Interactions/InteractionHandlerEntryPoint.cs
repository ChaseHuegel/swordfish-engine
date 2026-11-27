using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Statistics;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// Builds the interaction pipeline and stat modifier resolver once the module set is complete and logs what
/// they contain. Both resolve every registered contributor from DI, so this entry point is where late-loaded
/// modules become part of the shared parent singletons before any world container is created.
/// </summary>
public sealed class InteractionHandlerEntryPoint(
    IInteractionHandlerRegistry registry,
    IStatResolver statResolver,
    ILogger<InteractionHandlerEntryPoint> logger
) : IEntryPoint
{
    /// <inheritdoc/>
    public void Run()
    {
        logger.LogInformation(
            "Loaded interaction handlers: select {select}, resolve {resolve}, validate {validate}, commit {commit}, react {react}; stat modifier sources: {statSources}.",
            registry.GetHandlers(InteractionStage.Select).Count,
            registry.GetHandlers(InteractionStage.Resolve).Count,
            registry.GetHandlers(InteractionStage.Validate).Count,
            registry.GetHandlers(InteractionStage.Commit).Count,
            registry.GetHandlers(InteractionStage.React).Count,
            statResolver.GetSources().Count);
    }
}
