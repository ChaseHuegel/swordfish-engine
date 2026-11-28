using System.Collections.Generic;

namespace WaywardBeyond.Gameplay.Stations;

/// <summary>Ordered registry dispatching staged <see cref="WaywardBeyond.Data.Components.StationActionOp"/> commands to handlers.</summary>
public interface IStationActionHandlerRegistry
{
    /// <summary>Dispatches <paramref name="context"/> to matching handlers in priority and registration order.</summary>
    StationActionResult Dispatch(in StationActionContext context);

    /// <summary>Returns the ordered snapshot of all registered station action handlers.</summary>
    IReadOnlyList<IStationActionHandler> GetHandlers();
}
