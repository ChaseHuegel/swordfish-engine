using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.Statistics;

internal static class StatisticExtensions
{
    public static Int2 AddStatistic(this ref Character character, string id, int value)
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
            statistic = new Statistic(id, 0);
        }

        if (statisticIndex == -1)
        {
            statisticIndex = statistics.Length;
                
            Statistic[] oldArr = statistics;
            statistics = new Statistic[oldArr.Length + 1];
            oldArr.CopyTo(statistics, 0);
        }

        int prevValue = statistic.Value;
        statistic.Value += value;
        statistics[statisticIndex] = statistic;
        
        character.Statistics = statistics;
        
        return new Int2(prevValue, statistic.Value);
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
}