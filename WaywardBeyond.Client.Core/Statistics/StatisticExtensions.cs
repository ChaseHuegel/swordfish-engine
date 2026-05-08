using System;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.Statistics;

internal static class StatisticExtensions
{
    public static long GetStatistic(this Character character, string id)
    {
        if (character.Statistics == null)
        {
            return 0;
        }

        if (character.Statistics.TryGet(id, out var statistic))
        {
            return statistic.Value;
        }
        
        return 0;
    }
    
    public static StatisticInfo AddStatistic(this ref Character character, string id, long value)
    {
        return character.UpdateStatistic(id, value, StatisticOperation.Add);
    }

    public static StatisticInfo SetStatistic(this ref Character character, string id, long value)
    {
        return character.UpdateStatistic(id, value, StatisticOperation.Set);
    }
    
    private static StatisticInfo UpdateStatistic(this ref Character character, string id, long value, StatisticOperation operation)
    {
        Statistic[] statistics = character.Statistics ?? [];
        
        int statisticIndex = -1;
        Statistic statistic = default;
        for (var n = 0; n < statistics.Length; n++)
        {
            Statistic curStatistic = statistics[n];
            if (curStatistic.ID != id)
            {
                continue;
            }
            
            statisticIndex = n;
            statistic = curStatistic;
            break;
        }

        if (statistic.ID == null)
        {
            statistic = new Statistic(id, operation == StatisticOperation.Set ? value : 0);
        }

        if (statisticIndex == -1)
        {
            statisticIndex = statistics.Length;
            
            Statistic[] oldArr = statistics;
            statistics = new Statistic[oldArr.Length + 1];
            oldArr.CopyTo(statistics, 0);
        }

        long prevValue = statistic.Value;

        switch (operation)
        {
            case StatisticOperation.Add:
                statistic.Value += value;
                break;
            case StatisticOperation.Set:
                statistic.Value = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
        
        statistics[statisticIndex] = statistic;
        character.Statistics = statistics;
        
        return new StatisticInfo(prevValue, statistic.Value);
    }
    
    public static bool TryGet(this Statistic[] statistics, string id, out Statistic statistic)
    {
        for (var n = 0; n < statistics.Length; n++)
        {
            statistic = statistics[n];
            if (statistic.ID != id)
            {
                continue;
            }
            
            return true;
        }
        
        statistic = default;
        return false;
    }

    private enum StatisticOperation
    {
        Add,
        Set,
    }
}