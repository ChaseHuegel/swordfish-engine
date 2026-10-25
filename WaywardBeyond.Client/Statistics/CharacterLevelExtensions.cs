using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Client.Statistics;

internal static class CharacterLevelExtensions
{
    /// <summary>Sums the resolved level of every skill recorded on the character.</summary>
    public static int GetTotalSkillLevel(this Character character, ISkillDatabase skillDatabase)
    {
        if (character.Statistics == null)
        {
            return 0;
        }

        var total = 0;
        for (var i = 0; i < character.Statistics.Length; i++)
        {
            Statistic statistic = character.Statistics[i];
            Result<Skill> skillResult = skillDatabase.Get(statistic.ID);
            if (!skillResult)
            {
                continue;
            }

            total += skillResult.Value.CalculateLevel(statistic.Value).Level;
        }

        return total;
    }
}
