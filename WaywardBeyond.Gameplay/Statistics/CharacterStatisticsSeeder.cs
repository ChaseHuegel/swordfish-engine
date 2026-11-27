using System;
using System.Collections.Generic;
using Krill;
using WaywardBeyond.Data;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>
/// Builds and seeds runtime <see cref="SkillStateComponent"/> and <see cref="StatisticsComponent"/> from saved
/// character statistics so client and server evaluate statistics identically through ECS components.
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

    /// <summary>Creates a <see cref="StatisticsComponent"/> with every non-skill numeric statistic from the character save.</summary>
    public static StatisticsComponent CreateStatistics(Statistic[]? statistics, ISkillDatabase? skillDatabase)
    {
        var values = new Dictionary<string, float>(StringComparer.Ordinal);
        if (statistics != null)
        {
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

                values[statistic.ID] = statistic.Value;
            }
        }

        return new StatisticsComponent(values);
    }

    /// <summary>Seeds <see cref="SkillStateComponent"/> onto <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static void SeedSkillState(DataStore store, int entity, Statistic[]? statistics, ISkillDatabase? skillDatabase)
    {
        store.AddOrUpdate(entity, CreateSkillState(statistics, skillDatabase));
    }

    /// <summary>Seeds <see cref="StatisticsComponent"/> onto <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static void SeedStatistics(DataStore store, int entity, Statistic[]? statistics, ISkillDatabase? skillDatabase)
    {
        store.AddOrUpdate(entity, CreateStatistics(statistics, skillDatabase));
    }
}
