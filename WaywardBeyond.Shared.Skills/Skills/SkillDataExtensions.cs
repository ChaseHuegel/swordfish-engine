using System.Collections.Generic;

namespace WaywardBeyond.Shared.Skills;

public static class SkillDataExtensions
{
    public static LevelInfo CalculateLevel(this SkillData skill, long currentXP)
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