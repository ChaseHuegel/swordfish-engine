using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data.Localization;

namespace WaywardBeyond.Skills;

/// <inheritdoc/>
internal sealed class SkillDatabase : VirtualAssetDatabase<SkillDefinitions, SkillDefinition, Skill>, IAutoActivate, ISkillDatabase
{
    private readonly IBrickRegistry _brickRegistry;
    private readonly LocalizedTags? _invariantTags;
    private readonly Dictionary<XPSource, List<Skill>> _skillsBySource = [];

    /// <summary>Constructs a skill database.</summary>
    public SkillDatabase(
        in ILogger<SkillDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs,
        in IBrickRegistry brickRegistry,
        in ILocalizedTagsDatabase tagDatabase
    ) : base(logger, fileParseService, vfs)
    {
        _brickRegistry = brickRegistry;
        _invariantTags = tagDatabase.Invariant;
        Load();
    }

    /// <inheritdoc/>
    public IReadOnlyList<Skill> Get(XPSource xpSource)
    {
        return _skillsBySource.TryGetValue(xpSource, out List<Skill>? skills) ? skills : [];
    }

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => new("skills/");

    /// <inheritdoc/>
    protected override IEnumerable<SkillDefinition> GetAssetInfo(PathInfo path, SkillDefinitions resource) => resource.Skills ?? [];

    /// <inheritdoc/>
    protected override string GetAssetID(SkillDefinition assetInfo) => assetInfo.ID ?? string.Empty;

    /// <inheritdoc/>
    protected override Result<Skill> LoadAsset(string id, SkillDefinition assetInfo)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<Skill>.FromFailure("A skill is missing its ID");
        }

        if (Get(id).Success)
        {
            return Result<Skill>.FromFailure($"Skill ID \"{id}\" is declared more than once");
        }

        Dictionary<int, int>? declaredLevels = assetInfo.Levels;
        if (declaredLevels == null || declaredLevels.Count == 0)
        {
            return Result<Skill>.FromFailure("A skill carries no level curve");
        }

        foreach (KeyValuePair<int, int> level in declaredLevels)
        {
            if (level.Key < 1 || level.Value < 1)
            {
                return Result<Skill>.FromFailure(
                    $"Skill level {level.Key} requires {level.Value} XP; levels and their requirements must be at least 1");
            }
        }

        var sources = new Dictionary<XPSource, Dictionary<ushort, int>>();
        Dictionary<XPSource, Dictionary<string, int>>? declaredSources = assetInfo.Sources;
        if (declaredSources != null)
        {
            foreach (KeyValuePair<XPSource, Dictionary<string, int>> sourceGroup in declaredSources)
            {
                Dictionary<ushort, int> dataIDSources = ResolveSources(sourceGroup.Value, id);
                if (dataIDSources.Count > 0)
                {
                    sources[sourceGroup.Key] = dataIDSources;
                }
            }
        }

        if (sources.Count == 0)
        {
            Logger.LogWarning("Skill \"{id}\" grants no XP; no source resolved to a brick.", id);
        }

        var levels = new SortedDictionary<int, int>(declaredLevels);
        var skill = new Skill(id, assetInfo.Name ?? string.Empty, assetInfo.Category ?? string.Empty, assetInfo.Icon, sources, levels);

        foreach (XPSource kind in sources.Keys)
        {
            if (!_skillsBySource.TryGetValue(kind, out List<Skill>? skills))
            {
                skills = [];
                _skillsBySource[kind] = skills;
            }

            skills.Add(skill);
        }

        return Result<Skill>.FromSuccess(skill);
    }

    private Dictionary<ushort, int> ResolveSources(Dictionary<string, int> entries, string skillID)
    {
        var dataIDSources = new Dictionary<ushort, int>();
        foreach (KeyValuePair<string, int> entry in entries)
        {
            if (entry.Key.StartsWith("tag:", StringComparison.Ordinal))
            {
                //  Expand a tag into every brick id it lists, each mapped to the same XP amount.
                string tag = entry.Key[4..];
                IReadOnlyList<string>? brickIDs = _invariantTags?.GetValues(tag);
                if (brickIDs == null)
                {
                    Logger.LogWarning("Failed to find tag \"{tag}\" when parsing sources for skill ID \"{id}\".", tag, skillID);
                    continue;
                }

                for (var i = 0; i < brickIDs.Count; i++)
                {
                    AddSource(dataIDSources, brickIDs[i], entry.Value, skillID);
                }
            }
            else
            {
                AddSource(dataIDSources, entry.Key, entry.Value, skillID);
            }
        }

        return dataIDSources;
    }

    private void AddSource(Dictionary<ushort, int> sources, string brickID, int xp, string skillID)
    {
        ushort dataID = _brickRegistry.Id(brickID);
        if (dataID == 0)
        {
            Logger.LogWarning("Failed to find brick \"{brick}\" when parsing sources for skill ID \"{skill}\".", brickID, skillID);
            return;
        }

        if (sources.ContainsKey(dataID))
        {
            Logger.LogWarning(
                "Brick \"{brick}\" is listed more than once in the XP sources of skill \"{skill}\"; the last amount wins.",
                brickID, skillID);
        }

        sources[dataID] = xp;
    }
}
