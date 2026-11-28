using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Gameplay.Stations;

/// <summary>Stateless server-side handler for one or more <see cref="WaywardBeyond.Data.Components.StationActionOp"/> commands.</summary>
public interface IStationActionHandler
{
    /// <summary>The priority band controlling execution order across matching handlers.</summary>
    InteractionPriority Priority { get; }

    /// <summary>The filter restricting which commands this handler receives.</summary>
    StationActionFilter Filter { get; }

    /// <summary>Processes a validated station command and reports whether it was handled, declined, or rejected.</summary>
    StationActionResult Handle(in StationActionContext context);
}
