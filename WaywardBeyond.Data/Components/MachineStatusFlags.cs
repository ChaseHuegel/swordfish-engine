using System;

namespace WaywardBeyond.Data.Components;

/// <summary>Bit flags describing the runtime and persistent status of a <see cref="MachineStateComponent"/>.</summary>
[Flags]
public enum MachineStatusFlags : byte
{
    /// <summary>Idle with no active status conditions.</summary>
    None = 0,

    /// <summary>The machine is actively processing a recipe or generating output.</summary>
    Running = 1 << 0,

    /// <summary>The machine lacks sufficient power to operate.</summary>
    NoPower = 1 << 1,

    /// <summary>The machine's output inventory has no room for produced items.</summary>
    OutputFull = 1 << 2,

    /// <summary>A toggleable door or hatch brick-entity is open.</summary>
    Open = 1 << 3,

    /// <summary>Subset of status flags persisted across level saves and reloads.</summary>
    PersistentMask = Open,
}
