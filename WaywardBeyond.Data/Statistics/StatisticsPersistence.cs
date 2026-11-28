using System;
using System.Collections.Generic;

namespace WaywardBeyond.Data.Statistics;

/// <summary>Persistence projection and character-save merging helpers for entity statistics.</summary>
public static class StatisticsPersistence
{
    /// <summary>Captures surviving server-persisted stats from <paramref name="entries"/> for per-level save storage.</summary>
    public static Statistic[]? CaptureServerStatistics(ReadOnlySpan<Statistic> entries, long currentSimTick)
    {
        if (entries.IsEmpty)
        {
            return null;
        }

        var persisted = new List<Statistic>(entries.Length);
        for (var i = 0; i < entries.Length; i++)
        {
            if (TryNormalizePersistedServerEntry(in entries[i], currentSimTick, out Statistic normalized))
            {
                UpsertEntry(persisted, in normalized);
            }
        }

        return persisted.Count == 0 ? null : [.. persisted];
    }

    /// <summary>Merges durable replicated stats from <paramref name="source"/> into a character's saved statistics array.</summary>
    public static Statistic[]? MergeClientStatistics(Statistic[]? existing, ReadOnlySpan<Statistic> source)
    {
        if (source.IsEmpty)
        {
            return StripTransientCharacterEntries(existing);
        }

        var merged = new List<Statistic>((existing != null ? existing.Length : 0) + source.Length);
        CopyDurableEntries(existing, merged);

        for (var i = 0; i < source.Length; i++)
        {
            Statistic entry = source[i];
            if (entry.ID == null
                || !StatisticIds.IsNamespacedStatId(entry.ID.AsSpan())
                || IsTransientClientStat(entry.ID))
            {
                continue;
            }

            UpsertEntry(merged, in entry);
        }

        return merged.Count == 0 ? null : [.. merged];
    }

    /// <summary>Replaces an existing entry matching <see cref="Statistic.ID"/> or appends <paramref name="entry"/>.</summary>
    public static void UpsertEntry(List<Statistic> entries, in Statistic entry)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].ID, entry.ID, StringComparison.Ordinal))
            {
                entries[i] = entry;
                return;
            }
        }

        entries.Add(entry);
    }

    private static void CopyDurableEntries(Statistic[]? source, List<Statistic> destination)
    {
        if (source == null)
        {
            return;
        }

        for (var i = 0; i < source.Length; i++)
        {
            Statistic entry = source[i];
            if (!string.IsNullOrWhiteSpace(entry.ID) && !IsTransientClientStat(entry.ID))
            {
                UpsertEntry(destination, in entry);
            }
        }
    }

    private static bool TryNormalizePersistedServerEntry(in Statistic entry, long currentSimTick, out Statistic persisted)
    {
        if (string.IsNullOrWhiteSpace(entry.ID))
        {
            persisted = default;
            return false;
        }

        if (string.Equals(entry.ID, StatisticIds.PRESSURIZED, StringComparison.Ordinal))
        {
            persisted = default;
            return false;
        }

        if (StatisticIds.IsCooldownKey(entry.ID.AsSpan()))
        {
            persisted = default;
            return false;
        }

        if (StatisticIds.TryParseBuffId(entry.ID.AsSpan(), out ReadOnlySpan<char> buffId))
        {
            if (entry.Value <= currentSimTick)
            {
                persisted = default;
                return false;
            }

            ulong diff = unchecked((ulong)entry.Value - (ulong)currentSimTick);
            long remainingTicks = diff >= (ulong)long.MaxValue ? long.MaxValue : (long)diff;
            persisted = new Statistic(StatisticIds.BuffRemainingTicks(buffId), remainingTicks);
            return true;
        }

        if (StatisticIds.IsBuffRemainingTicksKey(entry.ID.AsSpan()))
        {
            if (entry.Value <= 0L)
            {
                persisted = default;
                return false;
            }

            persisted = entry;
            return true;
        }

        persisted = entry;
        return true;
    }

    private static bool IsTransientClientStat(string id)
    {
        return string.Equals(id, StatisticIds.PRESSURIZED, StringComparison.Ordinal)
            || StatisticIds.IsBuffTimerKey(id.AsSpan())
            || StatisticIds.IsCooldownKey(id.AsSpan());
    }

    private static Statistic[]? StripTransientCharacterEntries(Statistic[]? existing)
    {
        if (existing == null || existing.Length == 0)
        {
            return null;
        }

        var needsRewrite = false;
        for (var i = 0; i < existing.Length; i++)
        {
            string? id = existing[i].ID;
            if (string.IsNullOrWhiteSpace(id) || IsTransientClientStat(id))
            {
                needsRewrite = true;
                break;
            }
        }

        if (!needsRewrite)
        {
            return existing;
        }

        var filtered = new List<Statistic>(existing.Length);
        CopyDurableEntries(existing, filtered);
        return filtered.Count == 0 ? null : [.. filtered];
    }
}
