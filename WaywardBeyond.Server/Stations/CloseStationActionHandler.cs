using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Stations;

namespace WaywardBeyond.Server.Stations;

/// <summary>Built-in station action handler for <see cref="StationActionKind.CloseStation"/>.</summary>
public sealed class CloseStationActionHandler : IStationActionHandler
{
    /// <inheritdoc/>
    public InteractionPriority Priority => InteractionPriority.Normal;

    /// <inheritdoc/>
    public StationActionFilter Filter => new(Action: StationActionKind.CloseStation);

    /// <inheritdoc/>
    public StationActionResult Handle(in StationActionContext context)
    {
        if (context.Stations == null)
        {
            return StationActionResult.Reject("Station session service is unavailable.");
        }

        context.Stations.CloseStation(context.Store, context.Player, context.ClientId);
        return StationActionResult.Handled;
    }
}
