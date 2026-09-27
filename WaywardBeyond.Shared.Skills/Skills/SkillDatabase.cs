using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Skills;

/// <summary>
/// Headless, shared skill database. Loads the skill definition tomls from the virtual <c>skills/</c> root
/// and expands each skill's sources into brick data ids: a <c>tag:&lt;name&gt;</c> key is expanded through
/// the invariant tag lists under <c>lang/tags/</c>, and every brick id is hashed to its voxel data id via
/// <see cref="FNV1a.ComputeDataID"/>. Never touches localization, textures, or icons - it exists so the
/// authoritative server can run skill mechanics without any client-coupled asset pipeline.
/// </summary>
public sealed class SkillDatabase : VirtualAssetDatabase<SkillDefinitions, SkillDefinition, SkillData>, IAutoActivate
{
    private readonly ILogger<SkillDatabase> _logger;
    private readonly Dictionary<string, List<string>> _invariantTags = [];
    private readonly Dictionary<XPSource, HashSet<string>> _skillIDByXPSource =
        new()
        {
            { XPSource.Place, [] },
            { XPSource.Break, [] },
        };

    public SkillDatabase(
        in ILogger<SkillDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs
    ) : base(logger, fileParseService, vfs)
    {
        _logger = logger;
        LoadInvariantTags();
        Load();
    }

    /// <summary>
    /// Gets all skills that grant XP from the provided <see cref="XPSource"/>.
    /// </summary>
    public Result<SkillData[]> Get(XPSource xpSource)
    {
        if (!_skillIDByXPSource.TryGetValue(xpSource, out HashSet<string>? skillIDs))
        {
            return Result<SkillData[]>.FromFailure($"No skills found for XP source \"{xpSource}\"");
        }

        var skills = new SkillData[skillIDs.Count];
        var i = 0;
        foreach (string id in skillIDs)
        {
            skills[i] = Get(id).Value;
            i++;
        }

        return Result<SkillData[]>.FromSuccess(skills);
    }

    /// <summary>
    /// Whether the provided id names a loaded skill. Used when filtering a character's statistics into
    /// skill XP during the join-time seed.
    /// </summary>
    public bool HasSkill(string skillID)
    {
        return Get(skillID).Success;
    }

    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");

    protected override PathInfo GetRootPath() => new PathInfo("skills/");

    protected override IEnumerable<SkillDefinition> GetAssetInfo(PathInfo path, SkillDefinitions resource) => resource.Skills;

    protected override string GetAssetID(SkillDefinition assetInfo) => assetInfo.ID;

    protected override Result<SkillData> LoadAsset(string id, SkillDefinition assetInfo)
    {
        var sources = new Dictionary<XPSource, Dictionary<ushort, int>>
        {
            { XPSource.Place, [] },
            { XPSource.Break, [] },
        };

        foreach (KeyValuePair<XPSource, Dictionary<string, int>> sourceGroup in assetInfo.Sources)
        {
            XPSource kind = sourceGroup.Key;
            Dictionary<ushort, int> dataIDSources = sources[kind];

            foreach (KeyValuePair<string, int> source in sourceGroup.Value)
            {
                if (source.Key.StartsWith("tag:", System.StringComparison.Ordinal))
                {
                    //  Expand a tag into every brick id it lists, each mapped to the same XP amount.
                    string tag = source.Key[4..];
                    if (!_invariantTags.TryGetValue(tag, out List<string>? brickIDs))
                    {
                        _logger.LogWarning("Failed to find tag \"{tag}\" when parsing sources for skill ID \"{id}\".", tag, id);
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

            _skillIDByXPSource[kind].Add(id);
        }

        //  Ensure the level curve is ordered by level and derive the max level.
        Dictionary<int, int> orderedLevels = assetInfo.Levels.OrderBy(kvp => kvp.Key).ToDictionary();
        int maxLevel = orderedLevels.Keys.LastOrDefault();

        var skill = new SkillData(id, assetInfo.Name, assetInfo.Category, assetInfo.Icon, maxLevel, sources, orderedLevels);
        return Result<SkillData>.FromSuccess(skill);
    }

    private void AddDataIDSource(Dictionary<ushort, int> dataIDSources, string brickID, int xp, string skillID)
    {
        ushort dataID = FNV1a.ComputeDataID(brickID);
        if (dataIDSources.TryGetValue(dataID, out int existing))
        {
            if (existing != xp)
            {
                _logger.LogWarning(
                    "Brick id \"{brick}\" maps to the same data id {dataID} as another brick in the XP sources of skill \"{skill}\"; the newer XP amount wins.",
                    brickID, dataID, skillID);
            }
        }

        dataIDSources[dataID] = xp;
    }

    /// <summary>
    /// Loads the invariant (empty-language) brick tag lists from <c>lang/tags/</c>. These are gameplay
    /// data, not presentation: they define which brick ids a tag expands to for skill XP sources.
    /// </summary>
    private void LoadInvariantTags()
    {
        PathInfo tagRoot = new PathInfo("lang/tags/");
        foreach (PathInfo file in VFS.GetFiles(tagRoot, SearchOption.AllDirectories))
        {
            if (!file.HasExtension(".toml"))
            {
                continue;
            }

            SkillTagDefinition definition;
            try
            {
                definition = FileParseService.Parse<SkillTagDefinition>(file);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to parse tag definitions from \"{file}\".", file);
                continue;
            }

            if (!string.IsNullOrEmpty(definition.TwoLetterISOLanguageName))
            {
                //  Language-scoped tag files are client presentation; gameplay uses only invariant tags.
                continue;
            }

            foreach (KeyValuePair<string, List<string>> tag in definition.Tags)
            {
                if (!_invariantTags.TryGetValue(tag.Key, out List<string>? brickIDs))
                {
                    brickIDs = [];
                    _invariantTags[tag.Key] = brickIDs;
                }

                for (var i = 0; i < tag.Value.Count; i++)
                {
                    if (!brickIDs.Contains(tag.Value[i]))
                    {
                        brickIDs.Add(tag.Value[i]);
                    }
                }
            }
        }
    }
}