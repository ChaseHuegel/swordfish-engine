using System;
using Krill;
using WaywardBeyond.Gameplay.Power;

namespace WaywardBeyond.Gameplay.Components;

/// <summary>Server-side power bus snapshot attached to a voxel structure entity.</summary>
public struct PowerGridComponent : IDataComponent
{
    /// <summary>Total enabled generation in kilowatts.</summary>
    public float SupplyKw;

    /// <summary>Total enabled requested demand in kilowatts, including shed demand.</summary>
    public float DemandKw;

    /// <summary>The authoritative balance state.</summary>
    public PowerGridStatus Status;

    /// <summary>The number of enabled producers included in <see cref="SupplyKw"/>.</summary>
    public int ProducerCount;

    /// <summary>The number of enabled consumers included in <see cref="DemandKw"/>.</summary>
    public int ConsumerCount;

    /// <summary>Gets the immutable telemetry view of this snapshot.</summary>
    public readonly GridPowerTelemetry Telemetry => new(SupplyKw, DemandKw, Status, ProducerCount, ConsumerCount);

    /// <summary>Returns whether this snapshot matches <paramref name="other"/> across every field.</summary>
    public readonly bool EqualsState(in PowerGridComponent other)
    {
        return BitConverter.SingleToInt32Bits(SupplyKw) == BitConverter.SingleToInt32Bits(other.SupplyKw)
            && BitConverter.SingleToInt32Bits(DemandKw) == BitConverter.SingleToInt32Bits(other.DemandKw)
            && Status == other.Status
            && ProducerCount == other.ProducerCount
            && ConsumerCount == other.ConsumerCount;
    }
}
