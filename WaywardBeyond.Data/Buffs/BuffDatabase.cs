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
internal sealed class BuffDatabase : VirtualAssetDatabase<BuffDefinitions, BuffEntry, BuffDefinition>, IAutoActivate, IBuffDatabase
{
    private readonly List<BuffDefinition> _allBuffs = [];
    private readonly ReadOnlyCollection<BuffDefinition> _readOnlyBuffs;

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
    public IReadOnlyList<BuffDefinition> All => _readOnlyBuffs;

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => new("buffs/");

    /// <inheritdoc/>
    protected override IEnumerable<BuffEntry> GetAssetInfo(PathInfo path, BuffDefinitions resource) => resource.Entries;

    /// <inheritdoc/>
    protected override string GetAssetID(BuffEntry assetInfo) => assetInfo.Definition.ID?.Trim() ?? string.Empty;

    /// <inheritdoc/>
    protected override Result<BuffDefinition> LoadAsset(string id, BuffEntry assetInfo)
    {
        if (assetInfo.Error != null)
        {
            return Result<BuffDefinition>.FromFailure(assetInfo.Error);
        }

        Result<BuffDefinition> validation = assetInfo.Definition.Validate();
        if (!validation.Success)
        {
            return validation;
        }

        BuffDefinition buff = validation.Value;
        if (Get(buff.ID).Success)
        {
            return Result<BuffDefinition>.FromFailure($"Buff ID \"{buff.ID}\" is declared more than once.");
        }

        _allBuffs.Add(buff);
        return Result<BuffDefinition>.FromSuccess(buff);
    }
}
