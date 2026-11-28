using System;
using Krill;
using WaywardBeyond.Data.Statistics;

namespace WaywardBeyond.Data.Components;

/// <summary>Server-only baseline player statistics and dirty-check fingerprints for stat aggregation.</summary>
public struct BaseStatisticsComponent : IDataComponent
{
    /// <summary>Baseline statistic entries captured before derived bonuses, gear, and buffs apply.</summary>
    public Statistic[]? Entries;

    /// <summary>Stat IDs most recently written by stat aggregation so unequipped/expired modifiers revert cleanly.</summary>
    public string[]? DerivedKeys;

    /// <summary>Fingerprint of equipped wearable and augment slots from the last aggregation pass.</summary>
    public ulong GearHash;

    /// <summary>Fingerprint of active buff IDs from the last aggregation pass.</summary>
    public ulong BuffHash;

    /// <summary>Earliest expiry sim tick among active buffs, or <see cref="long.MaxValue"/> when none are active.</summary>
    public long NextBuffExpiryTick;

    /// <summary>Attempts to read the baseline integer value for <paramref name="id"/>.</summary>
    public readonly bool TryGetValue(string? id, out long value)
    {
        if (string.IsNullOrEmpty(id) || Entries == null)
        {
            value = 0L;
            return false;
        }

        for (var i = 0; i < Entries.Length; i++)
        {
            if (string.Equals(Entries[i].ID, id, StringComparison.Ordinal))
            {
                value = Entries[i].Value;
                return true;
            }
        }

        value = 0L;
        return false;
    }

    /// <summary>Attempts to read a baseline fixed-point value in user floating-point units.</summary>
    public readonly bool TryGetFixed(string? id, out float value)
    {
        if (!TryGetValue(id, out long raw))
        {
            value = 0f;
            return false;
        }

        value = StatisticIds.FromFixed(raw);
        return true;
    }
}
