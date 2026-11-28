using System;

namespace WaywardBeyond.Gameplay.Power;

/// <summary>
/// Immutable per-structure power bus snapshot shared by pressurization, machine simulation, and the
/// WAY-OS computer backend.
/// </summary>
public readonly struct GridPowerTelemetry
{
    /// <summary>Creates a telemetry snapshot, clamping negative or non-finite values to zero.</summary>
    public GridPowerTelemetry(
        float supplyKw,
        float demandKw,
        PowerGridStatus status,
        int producerCount,
        int consumerCount
    ) {
        SupplyKw = Sanitize(supplyKw);
        DemandKw = Sanitize(demandKw);
        Status = status;
        ProducerCount = Math.Max(0, producerCount);
        ConsumerCount = Math.Max(0, consumerCount);
    }

    /// <summary>Total enabled generation in kilowatts.</summary>
    public float SupplyKw { get; }

    /// <summary>Total enabled requested demand in kilowatts, including shed demand during a brownout.</summary>
    public float DemandKw { get; }

    /// <summary>Supply minus demand in kilowatts; negative while the grid sheds load.</summary>
    public float NetKw => SupplyKw - DemandKw;

    /// <summary>
    /// Demand as a percentage of supply. Reports <c>100</c> when demand exists with no supply, and
    /// <c>0</c> when both are zero.
    /// </summary>
    public float LoadPercent => SupplyKw > 0f ? DemandKw / SupplyKw * 100f : DemandKw > 0f ? 100f : 0f;

    /// <summary>Whether the grid is shedding or cannot satisfy enabled demand.</summary>
    public bool Brownout => Status is PowerGridStatus.Brownout or PowerGridStatus.Overload;

    /// <summary>The number of enabled producers included in <see cref="SupplyKw"/>.</summary>
    public int ProducerCount { get; }

    /// <summary>The number of enabled consumers included in <see cref="DemandKw"/>.</summary>
    public int ConsumerCount { get; }

    /// <summary>The authoritative balance state.</summary>
    public PowerGridStatus Status { get; }

    private static float Sanitize(float value)
    {
        return float.IsFinite(value) && value > 0f ? value : 0f;
    }
}
