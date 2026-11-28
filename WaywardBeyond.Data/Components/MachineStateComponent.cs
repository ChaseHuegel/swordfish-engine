using System;
using Krill;

namespace WaywardBeyond.Data.Components;

/// <summary>Server-authoritative replicated runtime state for a brick-entity.</summary>
public partial struct MachineStateComponent : IDataComponent
{
    /// <summary>Creates a default machine state with empty station tag and power enabled.</summary>
    public MachineStateComponent()
        : this(
            _StationTag: string.Empty,
            _ActiveRecipeId: null,
            _Progress: 0f,
            _Duration: 0f,
            _PowerEnabled: true,
            _HasPower: false,
            _StatusFlags: (byte)MachineStatusFlags.None)
    {
    }

    /// <summary>Gets the typed status flags view of <see cref="StatusFlags"/>.</summary>
    public readonly MachineStatusFlags Flags => (MachineStatusFlags)StatusFlags;

    /// <summary>Returns whether all bits in <paramref name="flag"/> are set on <see cref="StatusFlags"/>.</summary>
    public readonly bool HasStatusFlag(MachineStatusFlags flag)
    {
        return (Flags & flag) == flag;
    }

    /// <summary>Returns whether this state matches <paramref name="other"/> across all replicated fields.</summary>
    public readonly bool EqualsState(in MachineStateComponent other)
    {
        return string.Equals(StationTag, other.StationTag, StringComparison.Ordinal)
            && string.Equals(ActiveRecipeId, other.ActiveRecipeId, StringComparison.Ordinal)
            && BitConverter.SingleToInt32Bits(Progress) == BitConverter.SingleToInt32Bits(other.Progress)
            && BitConverter.SingleToInt32Bits(Duration) == BitConverter.SingleToInt32Bits(other.Duration)
            && PowerEnabled == other.PowerEnabled
            && HasPower == other.HasPower
            && StatusFlags == other.StatusFlags;
    }

    /// <summary>Sets or clears <paramref name="flag"/>, returning whether <see cref="StatusFlags"/> changed.</summary>
    public bool SetStatusFlag(MachineStatusFlags flag, bool enabled)
    {
        byte next = enabled
            ? (byte)(StatusFlags | (byte)flag)
            : (byte)(StatusFlags & ~(byte)flag);
        if (next == StatusFlags)
        {
            return false;
        }

        StatusFlags = next;
        return true;
    }
}
