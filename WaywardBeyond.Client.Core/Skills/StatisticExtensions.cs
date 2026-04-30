using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.Skills;

internal static class StatisticExtensions
{
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
    
    public static Int2 Add(this Statistic[] statistics, string id, int value)
    {
        int statisticIndex = -1;
        Statistic statistic = default;
        for (var n = 0; n < statistics.Length; n++)
        {
            statistic = statistics[n];
            if (statistic.ID != id)
            {
                continue;
            }
            
            statisticIndex = n;
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
        
        return new Int2(prevValue, statistic.Value);
    }
}