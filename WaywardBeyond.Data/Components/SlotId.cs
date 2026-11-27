using System;

namespace WaywardBeyond.Data.Components;

/// <summary>Composite key addressing an inventory slot by group identifier and local slot index.</summary>
public partial struct SlotId : IEquatable<SlotId>
{
    /// <summary>Whether this slot address names a non-empty group and a non-negative local index.</summary>
    public readonly bool IsValid => !string.IsNullOrEmpty(GroupId) && LocalIndex >= 0;

    /// <inheritdoc/>
    public readonly bool Equals(SlotId other)
    {
        return LocalIndex == other.LocalIndex
            && string.Equals(GroupId, other.GroupId, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj)
    {
        return obj is SlotId other && Equals(other);
    }

    /// <inheritdoc/>
    public readonly override int GetHashCode()
    {
        return HashCode.Combine(GroupId ?? string.Empty, LocalIndex);
    }

    /// <inheritdoc/>
    public readonly override string ToString()
    {
        return $"{GroupId}[{LocalIndex}]";
    }

    /// <summary>Compares two slot addresses for ordinal group and index equality.</summary>
    public static bool operator ==(SlotId left, SlotId right) => left.Equals(right);

    /// <summary>Compares two slot addresses for inequality.</summary>
    public static bool operator !=(SlotId left, SlotId right) => !left.Equals(right);
}
