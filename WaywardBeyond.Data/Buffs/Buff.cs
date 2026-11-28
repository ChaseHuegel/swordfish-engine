using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Statistics;

namespace WaywardBeyond.Data.Buffs;

/// <summary>Validated data-driven timed buff with stacking rules, stat modifiers, and periodic regen rates.</summary>
public readonly record struct Buff
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

    /// <summary>Per-second periodic stat changes applied while the buff has not expired.</summary>
    public IReadOnlyList<BuffRegenRate> RegenRates { get; init; }

    /// <summary>Precomputed <c>wb:buff.&lt;id&gt;.expires_tick</c> key for zero-allocation hot-path writes.</summary>
    public string ExpiryStatId { get; internal init; }

    /// <summary>Validates all fields and returns a normalized <see cref="Buff"/>.</summary>
    public Result<Buff> Validate()
    {
        if (string.IsNullOrWhiteSpace(ID))
        {
            return Result<Buff>.FromFailure("Buff is missing its ID.");
        }

        string id = ID.Trim();
        if (string.IsNullOrWhiteSpace(Name))
        {
            return Result<Buff>.FromFailure($"Buff \"{id}\" is missing its name localization key.");
        }

        if (!float.IsFinite(DurationSeconds) || DurationSeconds <= 0f)
        {
            return Result<Buff>.FromFailure($"Buff \"{id}\" DurationSeconds must be finite and > 0.");
        }

        if (!Enum.IsDefined(StackPolicy))
        {
            return Result<Buff>.FromFailure($"Buff \"{id}\" has invalid StackPolicy \"{StackPolicy}\".");
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
                    return Result<Buff>.FromFailure(modResult.Message);
                }

                copy[i] = modResult.Value;
            }

            validatedModifiers = Array.AsReadOnly(copy);
        }

        IReadOnlyList<BuffRegenRate>? rawRegenRates = RegenRates;
        IReadOnlyList<BuffRegenRate> validatedRegenRates;
        if (rawRegenRates == null || rawRegenRates.Count == 0)
        {
            validatedRegenRates = ReadOnlyCollection<BuffRegenRate>.Empty;
        }
        else
        {
            var copy = new BuffRegenRate[rawRegenRates.Count];
            for (var i = 0; i < rawRegenRates.Count; i++)
            {
                Result<BuffRegenRate> regenResult = rawRegenRates[i].Validate(id);
                if (!regenResult.Success)
                {
                    return Result<Buff>.FromFailure(regenResult.Message);
                }

                copy[i] = regenResult.Value;
            }

            validatedRegenRates = Array.AsReadOnly(copy);
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
            RegenRates = validatedRegenRates,
            ExpiryStatId = StatisticIds.BuffExpiresTick(id),
        };
        return Result<Buff>.FromSuccess(normalized);
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
