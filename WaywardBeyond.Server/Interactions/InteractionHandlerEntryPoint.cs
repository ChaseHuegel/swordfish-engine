using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Stations;
using WaywardBeyond.Gameplay.Statistics;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// Builds the interaction pipeline, station action handler registry, and stat modifier resolver once
/// the module set is complete and logs what they contain. All resolve every registered contributor
/// from DI, so this entry point is where late-loaded modules become part of the shared parent
/// singletons before any world container is created.
/// </summary>
public sealed class InteractionHandlerEntryPoint(
    IInteractionHandlerRegistry registry,
    IStationActionHandlerRegistry stationActionRegistry,
    IStatResolver statResolver,
    ILogger<InteractionHandlerEntryPoint> logger
) : IEntryPoint
{
    /// <inheritdoc/>
    public void Run()
    {
        logger.LogInformation(
            "Loaded interaction handlers: select {select}, resolve {resolve}, validate {validate}, commit {commit}, react {react}; station action handlers: {stationActions}; stat modifier sources: {statSources}.",
            registry.GetHandlers(InteractionStage.Select).Count,
            registry.GetHandlers(InteractionStage.Resolve).Count,
            registry.GetHandlers(InteractionStage.Validate).Count,
            registry.GetHandlers(InteractionStage.Commit).Count,
            registry.GetHandlers(InteractionStage.React).Count,
            stationActionRegistry.GetHandlers().Count,
            statResolver.GetSources().Count);
    }
}
