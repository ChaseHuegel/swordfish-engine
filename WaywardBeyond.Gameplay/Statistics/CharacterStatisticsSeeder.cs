using System;
using System.Collections.Generic;
using Krill;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>
/// Builds and seeds runtime <see cref="SkillStateComponent"/> and <see cref="StatisticsComponent"/>
/// for both client pre-sync views and server-authoritative player joins.
/// </summary>
public static class CharacterStatisticsSeeder
{
    /// <summary>Creates a <see cref="SkillStateComponent"/> with every entry whose id names a known skill.</summary>
    public static SkillStateComponent CreateSkillState(Statistic[]? statistics, ISkillDatabase? skillDatabase)
    {
        var xpBySkillId = new Dictionary<string, long>(StringComparer.Ordinal);
        if (statistics != null && skillDatabase != null)
        {
            for (var i = 0; i < statistics.Length; i++)
            {
                Statistic statistic = statistics[i];
                if (!string.IsNullOrWhiteSpace(statistic.ID) && skillDatabase.Get(statistic.ID).Success)
                {
                    xpBySkillId[statistic.ID] = statistic.Value;
                }
            }
        }

        return new SkillStateComponent(xpBySkillId);
    }

    /// <summary>Creates a pre-sync <see cref="StatisticsComponent"/> with every non-skill statistic from a character save.</summary>
    public static StatisticsComponent CreateStatistics(Statistic[]? statistics, ISkillDatabase? skillDatabase)
    {
        if (statistics == null || statistics.Length == 0)
        {
            return new StatisticsComponent();
        }

        var entries = new List<Statistic>(statistics.Length);
        for (var i = 0; i < statistics.Length; i++)
        {
            Statistic statistic = statistics[i];
            if (string.IsNullOrWhiteSpace(statistic.ID))
            {
                continue;
            }

            if (skillDatabase != null && skillDatabase.Get(statistic.ID).Success)
            {
                continue;
            }

            StatisticsPersistence.UpsertEntry(entries, in statistic);
        }

        return entries.Count == 0 ? new StatisticsComponent() : new StatisticsComponent([.. entries]);
    }

    /// <summary>
    /// Creates a server-authoritative <see cref="StatisticsComponent"/> from per-level server persistence
    /// or a joining client's initial state, applying authoritative defaults, bounds, and buff restoration.
    /// </summary>
    public static StatisticsComponent CreateAuthoritativeStatistics(
        in CharacterState seed,
        GameMode gameMode,
        CharacterEntityData? persistedEntity = null,
        long currentSimTick = 0L
    )
    {
        Statistic[]? persisted = persistedEntity != null ? persistedEntity.Value.Statistics : null;
        bool hasPersisted = persisted != null;

        long healthMax = StatisticIds.DEFAULT_HEALTH_MAX_FIXED;
        if (hasPersisted && TryFindStatistic(persisted, StatisticIds.HEALTH_MAX, out long savedHealthMax))
        {
            healthMax = Math.Clamp(savedHealthMax, StatisticIds.FIXED_POINT_SCALE, StatisticIds.MAX_VITAL_FIXED);
        }

        long oxygenMax = StatisticIds.DEFAULT_OXYGEN_MAX_FIXED;
        if (hasPersisted && TryFindStatistic(persisted, StatisticIds.OXYGEN_MAX, out long savedOxygenMax))
        {
            oxygenMax = Math.Clamp(savedOxygenMax, StatisticIds.FIXED_POINT_SCALE, StatisticIds.MAX_VITAL_FIXED);
        }

        Span<long> clientAttributes = stackalloc long[StatisticIds.BaseAttributeIds.Length];
        ClientSeed clientSeed = ExtractClientSeed(seed.Statistics, clientAttributes);

        long health = healthMax;
        long oxygen = oxygenMax;
        if (gameMode != GameMode.Creative)
        {
            if (hasPersisted && TryFindStatistic(persisted, StatisticIds.HEALTH, out long savedHealth))
            {
                health = Math.Clamp(savedHealth, 0L, healthMax);
            }
            else if (clientSeed.Health.HasValue)
            {
                health = Math.Clamp(clientSeed.Health.Value, 0L, healthMax);
            }

            if (hasPersisted && TryFindStatistic(persisted, StatisticIds.OXYGEN, out long savedOxygen))
            {
                oxygen = Math.Clamp(savedOxygen, 0L, oxygenMax);
            }
            else if (clientSeed.Oxygen.HasValue)
            {
                oxygen = Math.Clamp(clientSeed.Oxygen.Value, 0L, oxygenMax);
            }
        }

        int initialCapacity = 5 + StatisticIds.BaseAttributeIds.Length + (persisted != null ? persisted.Length : 0);
        var entries = new List<Statistic>(initialCapacity)
        {
            new(StatisticIds.HEALTH_MAX, healthMax),
            new(StatisticIds.HEALTH, health),
            new(StatisticIds.OXYGEN_MAX, oxygenMax),
            new(StatisticIds.OXYGEN, oxygen),
            new(StatisticIds.PRESSURIZED, 0L),
        };

        for (var i = 0; i < StatisticIds.BaseAttributeIds.Length; i++)
        {
            string attrId = StatisticIds.BaseAttributeIds[i];
            long attrValue = StatisticIds.MIN_ATTRIBUTE;
            if (hasPersisted && TryFindStatistic(persisted, attrId, out long savedAttr))
            {
                attrValue = Math.Max(StatisticIds.MIN_ATTRIBUTE, savedAttr);
            }
            else if ((clientSeed.AttributeMask & (1 << i)) != 0)
            {
                attrValue = Math.Clamp(clientAttributes[i], StatisticIds.MIN_ATTRIBUTE, StatisticIds.MAX_BASE_ATTRIBUTE);
            }

            entries.Add(new Statistic(attrId, attrValue));
        }

        if (persisted != null)
        {
            for (var i = 0; i < persisted.Length; i++)
            {
                Statistic entry = persisted[i];
                if (string.IsNullOrWhiteSpace(entry.ID) || IsReservedJoinStat(entry.ID))
                {
                    continue;
                }

                if (StatisticIds.IsBuffExpiresTickKey(entry.ID.AsSpan()))
                {
                    continue;
                }

                if (StatisticIds.TryParseBuffRemainingTicksId(entry.ID.AsSpan(), out ReadOnlySpan<char> buffId))
                {
                    if (entry.Value <= 0L)
                    {
                        continue;
                    }

                    long safeSimTick = Math.Max(0L, currentSimTick);
                    long restoredExpiry = safeSimTick > long.MaxValue - entry.Value
                        ? long.MaxValue
                        : safeSimTick + entry.Value;
                    StatisticsPersistence.UpsertEntry(entries, new Statistic(StatisticIds.BuffExpiresTick(buffId), restoredExpiry));
                    continue;
                }

                StatisticsPersistence.UpsertEntry(entries, in entry);
            }
        }

        return new StatisticsComponent([.. entries]);
    }

    /// <summary>Seeds <see cref="SkillStateComponent"/> onto <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static void SeedSkillState(DataStore store, int entity, Statistic[]? statistics, ISkillDatabase? skillDatabase)
    {
        store.AddOrUpdate(entity, CreateSkillState(statistics, skillDatabase));
    }

    /// <summary>Seeds a pre-sync <see cref="StatisticsComponent"/> onto <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static void SeedStatistics(DataStore store, int entity, Statistic[]? statistics, ISkillDatabase? skillDatabase)
    {
        store.AddOrUpdate(entity, CreateStatistics(statistics, skillDatabase));
    }

    /// <summary>Seeds a server-authoritative <see cref="StatisticsComponent"/> onto <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static void SeedAuthoritativeStatistics(
        DataStore store,
        int entity,
        in CharacterState seed,
        GameMode gameMode,
        CharacterEntityData? persistedEntity = null,
        long currentSimTick = 0L
    )
    {
        store.AddOrUpdate(entity, CreateAuthoritativeStatistics(in seed, gameMode, persistedEntity, currentSimTick));
    }

    private static ClientSeed ExtractClientSeed(Statistic[]? clientSeed, Span<long> attributes)
    {
        long? health = null;
        long? oxygen = null;
        var attributeMask = 0;

        if (clientSeed == null)
        {
            return new ClientSeed(health, oxygen, attributeMask);
        }

        int allAttributesMask = (1 << StatisticIds.BaseAttributeIds.Length) - 1;
        for (var i = 0; i < clientSeed.Length; i++)
        {
            if (health.HasValue && oxygen.HasValue && attributeMask == allAttributesMask)
            {
                break;
            }

            string? id = clientSeed[i].ID;
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            if (!health.HasValue && string.Equals(id, StatisticIds.HEALTH, StringComparison.Ordinal))
            {
                health = clientSeed[i].Value;
                continue;
            }

            if (!oxygen.HasValue && string.Equals(id, StatisticIds.OXYGEN, StringComparison.Ordinal))
            {
                oxygen = clientSeed[i].Value;
                continue;
            }

            for (var attrIndex = 0; attrIndex < StatisticIds.BaseAttributeIds.Length; attrIndex++)
            {
                int bit = 1 << attrIndex;
                if ((attributeMask & bit) == 0
                    && string.Equals(id, StatisticIds.BaseAttributeIds[attrIndex], StringComparison.Ordinal))
                {
                    attributeMask |= bit;
                    attributes[attrIndex] = clientSeed[i].Value;
                    break;
                }
            }
        }

        return new ClientSeed(health, oxygen, attributeMask);
    }

    private readonly record struct ClientSeed(long? Health, long? Oxygen, int AttributeMask);

    private static bool TryFindStatistic(Statistic[]? entries, string id, out long value)
    {
        if (entries == null)
        {
            value = 0L;
            return false;
        }

        for (var i = 0; i < entries.Length; i++)
        {
            if (string.Equals(entries[i].ID, id, StringComparison.Ordinal))
            {
                value = entries[i].Value;
                return true;
            }
        }

        value = 0L;
        return false;
    }

    private static bool IsReservedJoinStat(string id)
    {
        if (string.Equals(id, StatisticIds.HEALTH, StringComparison.Ordinal)
            || string.Equals(id, StatisticIds.HEALTH_MAX, StringComparison.Ordinal)
            || string.Equals(id, StatisticIds.OXYGEN, StringComparison.Ordinal)
            || string.Equals(id, StatisticIds.OXYGEN_MAX, StringComparison.Ordinal)
            || string.Equals(id, StatisticIds.PRESSURIZED, StringComparison.Ordinal))
        {
            return true;
        }

        for (var i = 0; i < StatisticIds.BaseAttributeIds.Length; i++)
        {
            if (string.Equals(id, StatisticIds.BaseAttributeIds[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
