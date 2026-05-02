using System.Collections.Generic;
using WaywardBeyond.Client.Core.Skills.Listeners;

namespace WaywardBeyond.Client.Core.Skills;

internal static class SkillExtensions
{
    public static LevelInfo CalculateLevel(this Skill skill, int currentXP)
    {
        var currentLevel = 0;
        var totalXP = 0;
        foreach (KeyValuePair<int, int> level in skill.Levels)
        {
            if (totalXP + level.Value < currentXP)
            {
                totalXP += level.Value;
                currentLevel = level.Key;
            }
            else
            {
                break;
            }
        }
        
        return new LevelInfo(currentLevel, currentXP - totalXP);
    }
}