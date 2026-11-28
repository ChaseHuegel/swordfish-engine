using System.Collections.Generic;

namespace WaywardBeyond.Data.Buffs;

/// <summary>TOML schema for a timed buff definition.</summary>
public struct BuffDefinition()
{
    /// <summary>Unique namespaced buff ID (for example <c>wb:well_fed</c>).</summary>
    public string? ID { get; set; }

    /// <summary>Localization key for the buff display name.</summary>
    public string? Name { get; set; }

    /// <summary>Optional localization key for the buff description.</summary>
    public string? Description { get; set; }

    /// <summary>Optional texture asset key for the buff icon.</summary>
    public string? Icon { get; set; }

    /// <summary>Base buff duration in seconds.</summary>
    public float DurationSeconds { get; set; }

    /// <summary>Optional stack policy name (<c>Refresh</c>, <c>Extend</c>, or <c>Ignore</c>; defaults to <c>Refresh</c>).</summary>
    public string? StackPolicy { get; set; }

    /// <summary>Optional stat modifiers active while the buff has not expired.</summary>
    public List<BuffStatModifierDefinition>? StatModifiers { get; set; }

    /// <summary>Optional per-second periodic stat changes applied while the buff has not expired.</summary>
    public List<BuffRegenRateDefinition>? RegenRates { get; set; }
}
