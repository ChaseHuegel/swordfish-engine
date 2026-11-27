using System;
using System.Collections.Generic;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Items;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
/// Shared deterministic duration math for Adventure Mode timed brick breaking.
/// </summary>
public static class BreakDuration
{
    /// <summary>Mining speed bonus added per matching skill level (2% per level).</summary>
    public const float SKILL_SPEED_BONUS_PER_LEVEL = 0.02f;

    /// <summary>Tolerance absorbing accumulated frame-delta floating-point drift at hold completion.</summary>
    public const float COMPLETION_EPSILON = 1e-5f;

    /// <summary>Computes the mining speed multiplier granted by a skill level.</summary>
    public static float CalculateSkillMultiplier(int skillLevel)
    {
        int clampedLevel = Math.Max(0, skillLevel);
        return 1.0f + (clampedLevel * SKILL_SPEED_BONUS_PER_LEVEL);
    }

    /// <summary>Returns whether elapsed hold time has reached <paramref name="requiredDuration"/>.</summary>
    public static bool IsComplete(float elapsed, float requiredDuration)
    {
        if (!float.IsFinite(requiredDuration) || requiredDuration <= 0f)
        {
            return true;
        }

        if (!float.IsFinite(elapsed) || elapsed < 0f)
        {
            return false;
        }

        float tolerance = Math.Min(COMPLETION_EPSILON, requiredDuration * COMPLETION_EPSILON);
        return elapsed + tolerance >= requiredDuration;
    }

    /// <summary>Normalizes elapsed hold time against <paramref name="requiredDuration"/> into <c>[0..1]</c>.</summary>
    public static float NormalizeProgress(float elapsed, float requiredDuration)
    {
        if (!float.IsFinite(requiredDuration) || requiredDuration <= 0f)
        {
            return 1f;
        }

        if (!float.IsFinite(elapsed) || elapsed <= 0f)
        {
            return 0f;
        }

        return Math.Clamp(elapsed / requiredDuration, 0f, 1f);
    }

    /// <summary>Computes break hold duration in seconds from hardness, tool speed, and skill level.</summary>
    public static float CalculateBreakDuration(float hardness, float toolMiningSpeed, int skillLevel)
    {
        float effectiveHardness = Brick.ResolveHardness(hardness);
        if (effectiveHardness == 0f)
        {
            return 0f;
        }

        float effectiveToolSpeed = float.IsFinite(toolMiningSpeed) && toolMiningSpeed > 0f
            ? toolMiningSpeed
            : ToolDefinition.DEFAULT_MINING_SPEED;

        float divisor = effectiveToolSpeed * CalculateSkillMultiplier(skillLevel);
        if (!float.IsFinite(divisor) || divisor <= 0f)
        {
            divisor = effectiveToolSpeed;
        }

        float duration = effectiveHardness / divisor;
        return float.IsFinite(duration) && duration >= 0f ? duration : effectiveHardness;
    }

    /// <summary>Resolves the effective break hardness of a brick id, falling back to <see cref="Brick.DEFAULT_HARDNESS"/>.</summary>
    public static float ResolveBrickHardness(IBrickDatabase? brickDatabase, ushort brickDataId)
    {
        if (brickDatabase == null || brickDataId == 0)
        {
            return Brick.DEFAULT_HARDNESS;
        }

        var result = brickDatabase.Get(brickDataId);
        return result.Success ? result.Value.Hardness : Brick.DEFAULT_HARDNESS;
    }

    /// <summary>Resolves the effective mining speed of a held item id, falling back to <see cref="ToolDefinition.DEFAULT_MINING_SPEED"/>.</summary>
    public static float ResolveToolMiningSpeed(IItemDatabase? itemDatabase, string? heldItemId)
    {
        if (itemDatabase == null || string.IsNullOrEmpty(heldItemId))
        {
            return ToolDefinition.DEFAULT_MINING_SPEED;
        }

        var result = itemDatabase.Get(heldItemId);
        if (!result.Success || !result.Value.Tool.HasValue)
        {
            return ToolDefinition.DEFAULT_MINING_SPEED;
        }

        return result.Value.Tool.Value.ResolveMiningSpeed();
    }

    /// <summary>Resolves the highest level across skills that award break XP for <paramref name="brickDataId"/>.</summary>
    public static int ResolveBreakSkillLevel(
        ISkillDatabase? skillDatabase,
        ushort brickDataId,
        IReadOnlyDictionary<string, long>? xpBySkill
    ) {
        return ResolveBreakSkillLevelCore(skillDatabase, brickDataId, new DictionaryXpLookup(xpBySkill));
    }

    /// <summary>Resolves the highest level across skills that award break XP for <paramref name="brickDataId"/>.</summary>
    public static int ResolveBreakSkillLevel(
        ISkillDatabase? skillDatabase,
        ushort brickDataId,
        Statistic[]? statistics
    ) {
        return ResolveBreakSkillLevelCore(skillDatabase, brickDataId, new StatisticsXpLookup(statistics));
    }

    /// <summary>Resolves deterministic break duration for a brick, held item, and dictionary-backed skill XP state.</summary>
    public static float ResolveBreakDuration(
        IBrickDatabase? brickDatabase,
        IItemDatabase? itemDatabase,
        ISkillDatabase? skillDatabase,
        ushort brickDataId,
        string? heldItemId,
        IReadOnlyDictionary<string, long>? xpBySkill
    ) {
        float hardness = ResolveBrickHardness(brickDatabase, brickDataId);
        float toolSpeed = ResolveToolMiningSpeed(itemDatabase, heldItemId);
        int skillLevel = ResolveBreakSkillLevel(skillDatabase, brickDataId, xpBySkill);
        return CalculateBreakDuration(hardness, toolSpeed, skillLevel);
    }

    /// <summary>Resolves deterministic break duration for a brick, held item, and character statistics array.</summary>
    public static float ResolveBreakDuration(
        IBrickDatabase? brickDatabase,
        IItemDatabase? itemDatabase,
        ISkillDatabase? skillDatabase,
        ushort brickDataId,
        string? heldItemId,
        Statistic[]? statistics
    ) {
        float hardness = ResolveBrickHardness(brickDatabase, brickDataId);
        float toolSpeed = ResolveToolMiningSpeed(itemDatabase, heldItemId);
        int skillLevel = ResolveBreakSkillLevel(skillDatabase, brickDataId, statistics);
        return CalculateBreakDuration(hardness, toolSpeed, skillLevel);
    }

    private static int ResolveBreakSkillLevelCore<TLookup>(
        ISkillDatabase? skillDatabase,
        ushort brickDataId,
        TLookup lookup
    ) where TLookup : struct, IXpLookup
    {
        if (skillDatabase == null || brickDataId == 0)
        {
            return 0;
        }

        IReadOnlyList<Skill> breakSkills = skillDatabase.Get(XPSource.Break);
        var highestLevel = 0;

        for (var i = 0; i < breakSkills.Count; i++)
        {
            Skill skill = breakSkills[i];
            if (!skill.GetXP(XPSource.Break, brickDataId).Success)
            {
                continue;
            }

            long totalXP = lookup.GetXP(skill.ID);
            int level = skill.CalculateLevel(totalXP).Level;
            if (level > highestLevel)
            {
                highestLevel = level;
            }
        }

        return highestLevel;
    }

    private interface IXpLookup
    {
        long GetXP(string skillId);
    }

    private readonly struct DictionaryXpLookup(IReadOnlyDictionary<string, long>? map) : IXpLookup
    {
        public long GetXP(string skillId)
        {
            return map != null && map.TryGetValue(skillId, out long xp) ? xp : 0L;
        }
    }

    private readonly struct StatisticsXpLookup(Statistic[]? statistics) : IXpLookup
    {
        public long GetXP(string skillId)
        {
            if (statistics == null)
            {
                return 0L;
            }

            for (var i = 0; i < statistics.Length; i++)
            {
                if (string.Equals(statistics[i].ID, skillId, StringComparison.Ordinal))
                {
                    return statistics[i].Value;
                }
            }

            return 0L;
        }
    }
}
