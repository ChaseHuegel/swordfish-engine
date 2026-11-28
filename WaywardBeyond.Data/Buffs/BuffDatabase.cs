using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Buffs;

/// <inheritdoc/>
internal sealed class BuffDatabase : VirtualAssetDatabase<BuffDefinitions, BuffDefinition, Buff>, IAutoActivate, IBuffDatabase
{
    private readonly List<Buff> _allBuffs = [];
    private readonly ReadOnlyCollection<Buff> _readOnlyBuffs;

    /// <summary>Constructs and loads the buff database from <c>buffs/*.toml</c>.</summary>
    public BuffDatabase(
        in ILogger<BuffDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs
    ) : base(logger, fileParseService, vfs)
    {
        _readOnlyBuffs = _allBuffs.AsReadOnly();
        Load();
        _allBuffs.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.ID, right.ID));
    }

    /// <inheritdoc/>
    public IReadOnlyList<Buff> All => _readOnlyBuffs;

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => new("buffs/");

    /// <inheritdoc/>
    protected override IEnumerable<BuffDefinition> GetAssetInfo(PathInfo path, BuffDefinitions resource)
    {
        if (resource.Buffs != null)
        {
            for (var i = 0; i < resource.Buffs.Count; i++)
            {
                yield return resource.Buffs[i];
            }
        }
    }

    /// <inheritdoc/>
    protected override string GetAssetID(BuffDefinition assetInfo) => assetInfo.ID?.Trim() ?? string.Empty;

    /// <inheritdoc/>
    protected override Result<Buff> LoadAsset(string id, BuffDefinition assetInfo)
    {
        Result<BuffStackPolicy> stackPolicyResult = ParseStackPolicy(id, assetInfo.StackPolicy);
        if (!stackPolicyResult.Success)
        {
            return Result<Buff>.FromFailure(stackPolicyResult.Message);
        }

        BuffStatModifier[] modifiers = ConvertModifiers(assetInfo.StatModifiers);
        BuffRegenRate[] regenRates = ConvertRegenRates(assetInfo.RegenRates);

        var candidate = new Buff
        {
            ID = assetInfo.ID ?? string.Empty,
            Name = assetInfo.Name ?? string.Empty,
            Description = assetInfo.Description,
            Icon = assetInfo.Icon,
            DurationSeconds = assetInfo.DurationSeconds,
            StackPolicy = stackPolicyResult.Value,
            StatModifiers = modifiers,
            RegenRates = regenRates,
            ExpiryStatId = string.Empty,
        };

        Result<Buff> validation = candidate.Validate();
        if (!validation.Success)
        {
            return validation;
        }

        Buff buff = validation.Value;
        if (Get(buff.ID).Success)
        {
            return Result<Buff>.FromFailure($"Buff ID \"{buff.ID}\" is declared more than once.");
        }

        _allBuffs.Add(buff);
        return Result<Buff>.FromSuccess(buff);
    }

    private static Result<BuffStackPolicy> ParseStackPolicy(string buffId, string? raw)
    {
        if (raw == null)
        {
            return Result<BuffStackPolicy>.FromSuccess(BuffStackPolicy.Refresh);
        }

        ReadOnlySpan<char> trimmed = raw.AsSpan().Trim();
        if (trimmed.Equals("Refresh".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return Result<BuffStackPolicy>.FromSuccess(BuffStackPolicy.Refresh);
        }

        if (trimmed.Equals("Extend".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return Result<BuffStackPolicy>.FromSuccess(BuffStackPolicy.Extend);
        }

        if (trimmed.Equals("Ignore".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return Result<BuffStackPolicy>.FromSuccess(BuffStackPolicy.Ignore);
        }

        return Result<BuffStackPolicy>.FromFailure($"Buff \"{buffId}\" has invalid StackPolicy \"{raw}\"; expected Refresh, Extend, or Ignore.");
    }

    private static BuffStatModifier[] ConvertModifiers(List<BuffStatModifierDefinition>? definitions)
    {
        if (definitions == null || definitions.Count == 0)
        {
            return [];
        }

        var result = new BuffStatModifier[definitions.Count];
        for (var i = 0; i < definitions.Count; i++)
        {
            BuffStatModifierDefinition def = definitions[i];
            result[i] = new BuffStatModifier(def.StatId ?? string.Empty, def.FlatDelta, def.PercentDelta);
        }

        return result;
    }

    private static BuffRegenRate[] ConvertRegenRates(List<BuffRegenRateDefinition>? definitions)
    {
        if (definitions == null || definitions.Count == 0)
        {
            return [];
        }

        var result = new BuffRegenRate[definitions.Count];
        for (var i = 0; i < definitions.Count; i++)
        {
            BuffRegenRateDefinition def = definitions[i];
            result[i] = new BuffRegenRate(def.StatId ?? string.Empty, def.PerSecond);
        }

        return result;
    }
}
