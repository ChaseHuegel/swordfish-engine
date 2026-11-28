using System;

namespace WaywardBeyond.Data.Components;

/// <summary>Payload of a single client-to-server station or self command.</summary>
public partial struct StationActionOp
{
    /// <summary>Maximum permitted character length for <see cref="ActionId"/> on the wire.</summary>
    public const int MAX_ACTION_ID_LENGTH = 128;

    /// <summary>Maximum permitted character length for <see cref="Argument"/> on the wire.</summary>
    public const int MAX_ARGUMENT_LENGTH = 256;

    /// <summary>Returns whether the action kind is defined and string fields fit within wire length limits.</summary>
    public readonly bool IsWithinWireBounds()
    {
        if (!Enum.IsDefined(Action) || Action == StationActionKind.None)
        {
            return false;
        }

        if (ActionId != null && ActionId.Length > MAX_ACTION_ID_LENGTH)
        {
            return false;
        }

        if (Argument != null && Argument.Length > MAX_ARGUMENT_LENGTH)
        {
            return false;
        }

        if (Action == StationActionKind.Custom && string.IsNullOrWhiteSpace(ActionId))
        {
            return false;
        }

        return true;
    }
}
