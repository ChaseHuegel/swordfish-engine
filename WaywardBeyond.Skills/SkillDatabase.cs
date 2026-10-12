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
        var sources = new Dictionary<XPSource, Dictionary<ushort, int>>();
        if (assetInfo.Sources != null)
        {
            foreach (KeyValuePair<XPSource, Dictionary<string, int>> sourceGroup in assetInfo.Sources)
            {
                XPSource kind = sourceGroup.Key;
                if (!sources.TryGetValue(kind, out Dictionary<ushort, int>? dataIDSources))
                {
                    dataIDSources = [];
                    sources[kind] = dataIDSources;
                }

                foreach (KeyValuePair<string, int> source in sourceGroup.Value)
                {
                    if (source.Key.StartsWith("tag:", StringComparison.Ordinal))
                    {
                        //  Expand a tag into every brick id it lists, each mapped to the same XP amount.
                        string tag = source.Key[4..];
                        IReadOnlyList<string>? brickIDs = _invariantTags?.GetValues(tag);
                        if (brickIDs == null)
                        {
                            Logger.LogWarning("Failed to find tag \"{tag}\" when parsing sources for skill ID \"{id}\".", tag, id);
                            continue;
                        }

                        for (var i = 0; i < brickIDs.Count; i++)
                        {
                            AddDataIDSource(dataIDSources, brickIDs[i], source.Value, id);
                        }
                    }
                    else
                    {
                        AddDataIDSource(dataIDSources, source.Key, source.Value, id);
                    }
                }
            }
        }

        var levels = new SortedDictionary<int, int>(assetInfo.Levels ?? []);
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

    private void AddDataIDSource(Dictionary<ushort, int> dataIDSources, string brickID, int xp, string skillID)
    {
        ushort dataID = _brickRegistry.Id(brickID);
        if (dataID == 0)
        {
            Logger.LogWarning("Failed to find brick \"{brick}\" when parsing sources for skill ID \"{skill}\".", brickID, skillID);
            return;
        }

        if (dataIDSources.TryGetValue(dataID, out int existing) && existing != xp)
        {
            Logger.LogWarning(
                "Brick id \"{brick}\" maps to the same data id {dataID} as another brick in the XP sources of skill \"{skill}\"; the newer XP amount wins.",
                brickID, dataID, skillID);
        }

        dataIDSources[dataID] = xp;
    }
}
