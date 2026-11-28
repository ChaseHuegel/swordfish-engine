using System;
using WaywardBeyond.Data.Statistics;

namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>Deterministic derived statistic bonuses granted per S.P.A.C.E.R. attribute point above <see cref="StatisticIds.MIN_ATTRIBUTE"/>.</summary>
public static class SpacerScaling
{
    /// <summary>Maximum health gained per Strength point above <see cref="StatisticIds.MIN_ATTRIBUTE"/>.</summary>
    public const float STRENGTH_HEALTH_MAX_PER_POINT = 5f;

    /// <summary>Maximum health gained per Resolve point above <see cref="StatisticIds.MIN_ATTRIBUTE"/>.</summary>
    public const float RESOLVE_HEALTH_MAX_PER_POINT = 5f;

    /// <summary>Kinetic collision resistance gained per Resolve point above <see cref="StatisticIds.MIN_ATTRIBUTE"/>.</summary>
    public const float RESOLVE_KINETIC_RESIST_PER_POINT = 0.02f;

    /// <summary>Maximum oxygen gained per Awareness point above <see cref="StatisticIds.MIN_ATTRIBUTE"/>.</summary>
    public const float AWARENESS_OXYGEN_MAX_PER_POINT = 5f;

    /// <summary>Movement speed percentage bonus gained per Precision point above <see cref="StatisticIds.MIN_ATTRIBUTE"/>.</summary>
    public const float PRECISION_MOVE_SPEED_PCT_PER_POINT = 0.02f;

    /// <summary>Mining speed bonus gained per Education point above <see cref="StatisticIds.MIN_ATTRIBUTE"/>.</summary>
    public const float EDUCATION_MINING_SPEED_PER_POINT = 0.02f;

    /// <summary>Computes the S.P.A.C.E.R. derived flat bonus for <paramref name="statId"/> from resolved attributes.</summary>
    public static float ComputeDerivedBonus(
        string statId,
        long strength,
        long precision,
        long awareness,
        long charisma,
        long education,
        long resolve
    )
    {
        _ = charisma;

        return statId switch
        {
            StatisticIds.HEALTH_MAX => (PointsAboveMin(strength) * STRENGTH_HEALTH_MAX_PER_POINT)
                + (PointsAboveMin(resolve) * RESOLVE_HEALTH_MAX_PER_POINT),
            StatisticIds.OXYGEN_MAX => PointsAboveMin(awareness) * AWARENESS_OXYGEN_MAX_PER_POINT,
            StatisticIds.KINETIC_RESIST => PointsAboveMin(resolve) * RESOLVE_KINETIC_RESIST_PER_POINT,
            StatisticIds.MOVE_SPEED_PCT => PointsAboveMin(precision) * PRECISION_MOVE_SPEED_PCT_PER_POINT,
            StatisticIds.MINING_SPEED => PointsAboveMin(education) * EDUCATION_MINING_SPEED_PER_POINT,
            _ => 0f,
        };
    }

    private static long PointsAboveMin(long attributeValue)
    {
        return Math.Max(0L, attributeValue - StatisticIds.MIN_ATTRIBUTE);
    }
}
