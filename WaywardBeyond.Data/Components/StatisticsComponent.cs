using System;
using Krill;
using WaywardBeyond.Data.Buffs;

namespace WaywardBeyond.Data.Components;

/// <summary>Server-authoritative replicated player statistics, vitals, attributes, and buff timers.</summary>
public partial struct StatisticsComponent : IDataComponent
{
    /// <summary>Creates an empty statistics component.</summary>
    public StatisticsComponent() : this([]) { }

    /// <summary>Gets the number of recorded stat entries.</summary>
    public readonly int Count => Entries != null ? Entries.Length : 0;

    /// <summary>Attempts to read the raw integer value for <paramref name="id"/>.</summary>
    public readonly bool TryGetValue(string? id, out long value)
    {
        if (string.IsNullOrEmpty(id))
        {
            value = 0L;
            return false;
        }

        return TryGetValue(id.AsSpan(), out value);
    }

    /// <summary>Attempts to read the raw integer value for <paramref name="id"/> without allocating.</summary>
    public readonly bool TryGetValue(ReadOnlySpan<char> id, out long value)
    {
        Statistic[]? entries = Entries;
        if (entries == null || id.IsEmpty)
        {
            value = 0L;
            return false;
        }

        for (var i = 0; i < entries.Length; i++)
        {
            string? entryId = entries[i].ID;
            if (entryId != null && entryId.AsSpan().Equals(id, StringComparison.Ordinal))
            {
                value = entries[i].Value;
                return true;
            }
        }

        value = 0L;
        return false;
    }

    /// <summary>Gets the raw integer value for <paramref name="id"/>, or <paramref name="defaultValue"/> when absent.</summary>
    public readonly long GetValue(string? id, long defaultValue = 0L)
    {
        return TryGetValue(id, out long value) ? value : defaultValue;
    }

    /// <summary>Attempts to read a fixed-point vital in user floating-point units.</summary>
    public readonly bool TryGetFixed(string? id, out float value)
    {
        if (!TryGetValue(id, out long raw))
        {
            value = 0f;
            return false;
        }

        value = StatIds.FromFixed(raw);
        return true;
    }

    /// <summary>Gets a fixed-point vital in user units, or <paramref name="defaultValue"/> when absent.</summary>
    public readonly float GetFixed(string? id, float defaultValue = 0f)
    {
        return TryGetValue(id, out long raw) ? StatIds.FromFixed(raw) : defaultValue;
    }

    /// <summary>Reads a boolean flag stat (<c>!= 0</c>), returning <c>false</c> when absent.</summary>
    public readonly bool GetFlag(string? id)
    {
        return TryGetValue(id, out long raw) && raw != 0L;
    }

    /// <summary>Attempts to read the authoritative expiry sim tick for <paramref name="buffId"/> without allocating.</summary>
    public readonly bool TryGetBuffExpiresTick(ReadOnlySpan<char> buffId, out long expiresTick)
    {
        int index = IndexOfBuff(buffId);
        if (index < 0)
        {
            expiresTick = 0L;
            return false;
        }

        expiresTick = Entries![index].Value;
        return true;
    }

    /// <summary>Computes remaining duration in seconds for <paramref name="buffId"/> without allocating.</summary>
    public readonly float GetBuffRemainingSeconds(ReadOnlySpan<char> buffId, long currentSimTick, float fixedDt)
    {
        return TryGetBuffExpiresTick(buffId, out long expiresTick)
            ? StatIds.ComputeRemainingSeconds(expiresTick, currentSimTick, fixedDt)
            : 0f;
    }

    /// <summary>Sets <paramref name="id"/> to <paramref name="value"/>, returning whether the state changed.</summary>
    public bool SetValue(string id, long value)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        Statistic[]? entries = Entries;
        if (entries == null)
        {
            Entries = [new Statistic(id, value)];
            return true;
        }

        for (var i = 0; i < entries.Length; i++)
        {
            if (!string.Equals(entries[i].ID, id, StringComparison.Ordinal))
            {
                continue;
            }

            if (entries[i].Value == value)
            {
                return false;
            }

            entries[i].Value = value;
            return true;
        }

        var updated = new Statistic[entries.Length + 1];
        entries.CopyTo(updated, 0);
        updated[entries.Length] = new Statistic(id, value);
        Entries = updated;
        return true;
    }

    /// <summary>Sets a fixed-point vital from user units, returning whether the fixed value changed.</summary>
    public bool SetFixed(string id, float value)
    {
        return StatIds.TryToFixed(value, out long fixedValue) && SetValue(id, fixedValue);
    }

    /// <summary>Sets a boolean flag stat (<c>1</c> or <c>0</c>), returning whether the flag changed.</summary>
    public bool SetFlag(string id, bool enabled)
    {
        return SetValue(id, enabled ? 1L : 0L);
    }

    /// <summary>Sets the expiry tick for <paramref name="buffId"/>, avoiding allocations when updating an active entry.</summary>
    public bool SetBuffExpiresTick(string buffId, long expiresTick)
    {
        if (string.IsNullOrWhiteSpace(buffId))
        {
            return false;
        }

        int existingIndex = IndexOfBuff(buffId.AsSpan());
        if (existingIndex >= 0)
        {
            Statistic[] entries = Entries!;
            if (entries[existingIndex].Value == expiresTick)
            {
                return false;
            }

            entries[existingIndex].Value = expiresTick;
            return true;
        }

        return SetValue(StatIds.BuffExpiresTick(buffId), expiresTick);
    }

    /// <summary>Applies <paramref name="buff"/> using its <see cref="BuffDefinition.StackPolicy"/> without allocating.</summary>
    public bool ApplyBuff(in BuffDefinition buff, long currentSimTick, float fixedDt)
    {
        if (string.IsNullOrWhiteSpace(buff.ExpiryStatId))
        {
            return false;
        }

        long existingExpiry = TryGetValue(buff.ExpiryStatId, out long current) ? current : 0L;
        if (!buff.TryComputeNextExpiresTick(existingExpiry, currentSimTick, fixedDt, out long nextExpiry))
        {
            return false;
        }

        return SetValue(buff.ExpiryStatId, nextExpiry);
    }

    /// <summary>Removes <paramref name="id"/> if present, returning whether an entry was removed.</summary>
    public bool Remove(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        return RemoveAt(IndexOf(id.AsSpan()));
    }

    /// <summary>Removes the expiry tick entry for <paramref name="buffId"/> without allocating on miss.</summary>
    public bool RemoveBuff(ReadOnlySpan<char> buffId)
    {
        return RemoveAt(IndexOfBuff(buffId));
    }

    /// <summary>Removes all expired buff entries (<c>expires_tick &lt;= currentSimTick</c>), allocating only if any expire.</summary>
    public bool PruneExpiredBuffs(long currentSimTick)
    {
        Statistic[]? entries = Entries;
        if (entries == null || entries.Length == 0)
        {
            return false;
        }

        var expiredCount = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            if (IsExpiredBuff(in entries[i], currentSimTick))
            {
                expiredCount++;
            }
        }

        if (expiredCount == 0)
        {
            return false;
        }

        if (expiredCount == entries.Length)
        {
            Entries = [];
            return true;
        }

        var remaining = new Statistic[entries.Length - expiredCount];
        var writeIndex = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            if (!IsExpiredBuff(in entries[i], currentSimTick))
            {
                remaining[writeIndex++] = entries[i];
            }
        }

        Entries = remaining;
        return true;
    }

    private readonly int IndexOf(ReadOnlySpan<char> id)
    {
        Statistic[]? entries = Entries;
        if (entries == null || id.IsEmpty)
        {
            return -1;
        }

        for (var i = 0; i < entries.Length; i++)
        {
            string? entryId = entries[i].ID;
            if (entryId != null && entryId.AsSpan().Equals(id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private readonly int IndexOfBuff(ReadOnlySpan<char> buffId)
    {
        Statistic[]? entries = Entries;
        if (entries == null || buffId.IsEmpty || buffId.IsWhiteSpace())
        {
            return -1;
        }

        for (var i = 0; i < entries.Length; i++)
        {
            string? entryId = entries[i].ID;
            if (entryId != null && StatIds.MatchesBuffExpiresTick(entryId.AsSpan(), buffId))
            {
                return i;
            }
        }

        return -1;
    }

    private bool RemoveAt(int index)
    {
        Statistic[]? entries = Entries;
        if (entries == null || index < 0 || index >= entries.Length)
        {
            return false;
        }

        if (entries.Length == 1)
        {
            Entries = [];
            return true;
        }

        var updated = new Statistic[entries.Length - 1];
        if (index > 0)
        {
            Array.Copy(entries, 0, updated, 0, index);
        }

        if (index + 1 < entries.Length)
        {
            Array.Copy(entries, index + 1, updated, index, entries.Length - index - 1);
        }

        Entries = updated;
        return true;
    }

    private static bool IsExpiredBuff(in Statistic entry, long currentSimTick)
    {
        return entry.ID != null
            && StatIds.IsBuffExpiresTickKey(entry.ID.AsSpan())
            && entry.Value <= currentSimTick;
    }
}
