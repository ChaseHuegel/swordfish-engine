using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Statistics;

namespace WaywardBeyond.Data.Buffs;

/// <summary>Defines a data-driven timed buff with stacking rules, stat modifiers, and vital regen rates.</summary>
public readonly record struct BuffDefinition
{
    /// <summary>Unique namespaced buff ID (for example <c>wb:well_fed</c>).</summary>
    public string ID { get; init; }

    /// <summary>Localization key for the buff display name.</summary>
    public string Name { get; init; }

    /// <summary>Optional localization key for the buff description.</summary>
    public string? Description { get; init; }

    /// <summary>Optional texture asset key for the buff icon.</summary>
    public string? Icon { get; init; }

    /// <summary>Base buff duration in seconds.</summary>
    public float DurationSeconds { get; init; }

    /// <summary>Policy applied when re-granting an already active buff.</summary>
    public BuffStackPolicy StackPolicy { get; init; }

    /// <summary>Stat modifiers active while the buff has not expired.</summary>
    public IReadOnlyList<BuffStatModifier> StatModifiers { get; init; }

    /// <summary>Passive health regeneration per second while active.</summary>
    public float HealthPerSec { get; init; }

    /// <summary>Passive oxygen regeneration per second while active.</summary>
    public float OxygenPerSec { get; init; }

    /// <summary>Precomputed <c>wb:buff.&lt;id&gt;.expires_tick</c> key for zero-allocation hot-path writes.</summary>
    public string ExpiryStatId { get; init; }

    /// <summary>Validates all fields and returns a normalized <see cref="BuffDefinition"/>.</summary>
    public Result<BuffDefinition> Validate()
    {
        if (string.IsNullOrWhiteSpace(ID))
        {
            return Result<BuffDefinition>.FromFailure("Buff definition is missing its ID.");
        }

        string id = ID.Trim();
        if (string.IsNullOrWhiteSpace(Name))
        {
            return Result<BuffDefinition>.FromFailure($"Buff \"{id}\" is missing its name localization key.");
        }

        if (!float.IsFinite(DurationSeconds) || DurationSeconds <= 0f)
        {
            return Result<BuffDefinition>.FromFailure($"Buff \"{id}\" duration_seconds must be finite and > 0.");
        }

        if (!Enum.IsDefined(StackPolicy))
        {
            return Result<BuffDefinition>.FromFailure($"Buff \"{id}\" has invalid stack_policy \"{StackPolicy}\".");
        }

        if (!float.IsFinite(HealthPerSec))
        {
            return Result<BuffDefinition>.FromFailure($"Buff \"{id}\" health_per_sec must be finite.");
        }

        if (!float.IsFinite(OxygenPerSec))
        {
            return Result<BuffDefinition>.FromFailure($"Buff \"{id}\" oxygen_per_sec must be finite.");
        }

        IReadOnlyList<BuffStatModifier>? rawModifiers = StatModifiers;
        IReadOnlyList<BuffStatModifier> validatedModifiers;
        if (rawModifiers == null || rawModifiers.Count == 0)
        {
            validatedModifiers = ReadOnlyCollection<BuffStatModifier>.Empty;
        }
        else
        {
            var copy = new BuffStatModifier[rawModifiers.Count];
            for (var i = 0; i < rawModifiers.Count; i++)
            {
                Result<BuffStatModifier> modResult = rawModifiers[i].Validate(id);
                if (!modResult.Success)
                {
                    return Result<BuffDefinition>.FromFailure(modResult.Message);
                }

                copy[i] = modResult.Value;
            }

            validatedModifiers = Array.AsReadOnly(copy);
        }

        string? description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        string? icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim();

        var normalized = this with
        {
            ID = id,
            Name = Name.Trim(),
            Description = description,
            Icon = icon,
            StatModifiers = validatedModifiers,
            ExpiryStatId = StatisticIds.BuffExpiresTick(id),
        };
        return Result<BuffDefinition>.FromSuccess(normalized);
    }

    /// <summary>Computes the next authoritative expiry tick under <see cref="StackPolicy"/>, returning false when unchanged.</summary>
    public bool TryComputeNextExpiresTick(
        long existingExpiresTick,
        long currentSimTick,
        float fixedDt,
        out long nextExpiresTick
    )
    {
        if (currentSimTick < 0L || DurationSeconds <= 0f || !float.IsFinite(DurationSeconds) || fixedDt <= 0f || !float.IsFinite(fixedDt))
        {
            nextExpiresTick = existingExpiresTick;
            return false;
        }

        switch (StackPolicy)
        {
            case BuffStackPolicy.Ignore:
                if (existingExpiresTick > currentSimTick)
                {
                    nextExpiresTick = existingExpiresTick;
                    return false;
                }

                nextExpiresTick = StatisticIds.ComputeExpiresTick(currentSimTick, DurationSeconds, fixedDt);
                return nextExpiresTick != existingExpiresTick;

            case BuffStackPolicy.Extend:
                long baseTick = existingExpiresTick > currentSimTick ? existingExpiresTick : currentSimTick;
                nextExpiresTick = StatisticIds.ComputeExpiresTick(baseTick, DurationSeconds, fixedDt);
                return nextExpiresTick != existingExpiresTick;

            case BuffStackPolicy.Refresh:
                nextExpiresTick = StatisticIds.ComputeExpiresTick(currentSimTick, DurationSeconds, fixedDt);
                return nextExpiresTick != existingExpiresTick;

            default:
                nextExpiresTick = existingExpiresTick;
                return false;
        }
    }
}
