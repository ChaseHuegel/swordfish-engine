using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Meta;

namespace WaywardBeyond.Client.Core.Skills;

/// <summary>
///     Provides access to skill information from virtual resources.
/// </summary>
internal sealed class SkillDatabase : VirtualAssetDatabase<SkillDefinitions, SkillDefinition, Skill>, IAutoActivate
{
    private readonly Shader _iconShader;
    private readonly Material _unknownIcon;
    private readonly ILogger<SkillDatabase> _logger;
    private readonly IAssetDatabase<Texture> _textureDatabase;
    private readonly IAssetDatabase<LocalizedTags> _localizedTagDatabase;
    private readonly ILocalization _localization;
    private readonly Dictionary<string, Material> _icons = [];

    public SkillDatabase(
        in ILogger<SkillDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs,
        in IAssetDatabase<Texture> textureDatabase,
        in IAssetDatabase<LocalizedTags> localizedTagDatabase,
        in ILocalization localization
    ) : base(logger, fileParseService, vfs)
    {
        _logger = logger;
        _textureDatabase = textureDatabase;
        _localizedTagDatabase = localizedTagDatabase;
        _localization = localization;
        _iconShader = fileParseService.Parse<Shader>(AssetPaths.Shaders.At("ui_reef_textured.glsl"));
        _unknownIcon = new Material(_iconShader, textureDatabase.Get("skills/unknown.png"));
        Load();
    }
    
    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");
    
    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Root.At("skills");
    
    /// <inheritdoc/>
    protected override IEnumerable<SkillDefinition> GetAssetInfo(PathInfo path, SkillDefinitions resource) => resource.Skills;

    /// <inheritdoc/>
    protected override string GetAssetID(SkillDefinition assetInfo) => assetInfo.ID;
    
    /// <inheritdoc/>
    protected override Result<Skill> LoadAsset(string id, SkillDefinition assetInfo)
    {
        //  Get an icon for the skill
        Material? icon;
        if (assetInfo.Icon == null)
        {
            icon = _unknownIcon;
        }
        else if (!_icons.TryGetValue(assetInfo.Icon, out icon))
        {
            Result<Texture> textureResult = _textureDatabase.Get(assetInfo.Icon);
            if (textureResult)
            {
                icon = new Material(_iconShader, textureResult);
                _icons[assetInfo.Icon] = icon;
            }
            else
            {
                Logger.LogError(textureResult, "Failed to get the icon \"{icon}\" for skill \"{skill}\".", assetInfo.Icon, id);
                icon = _unknownIcon;
            }
        }
        
        //  TODO when allowing language to be changed at runtime, these assets need to be reinitialized
        //  Localize the display name if a translation exists
        string localizedName = _localization.GetString(assetInfo.Name) ?? assetInfo.Name;
        string localizedCategory = _localization.GetString(assetInfo.Category) ?? assetInfo.Category;

        XPSources xpSources = new XPSources();
        foreach (KeyValuePair<string, int> source in assetInfo.Sources.Break)
        {
            //  If this source has a type, try to parse it.
            int separatorIndex = source.Key.IndexOf(':');
            if (separatorIndex != -1 && separatorIndex > 0)
            {
                string type = source.Key[..separatorIndex];
                string value = source.Key[(separatorIndex + 1)..];
                
                switch (type)
                {
                    case "tag":
                        //  Find the lang tags
                        Result<LocalizedTags> localizedTag = _localizedTagDatabase.Get(CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
                        if (!localizedTag.Success)
                        {
                            _logger.LogWarning("Failed to find tag \"{tag}\" for lang \"{lang}\" when parsing sources for skill ID \"{id}\".", value, CultureInfo.CurrentCulture.TwoLetterISOLanguageName, id);
                            continue;
                        }

                        //  Find the tag
                        if (!localizedTag.Value.Tags.TryGetValue(value, out List<string>? tagValues))
                        {
                            _logger.LogWarning("Failed to find tag \"{tag}\" for lang \"{lang}\" when parsing sources for skill ID \"{id}\".", value, CultureInfo.CurrentCulture.TwoLetterISOLanguageName, id);
                            continue;
                        }

                        //  Add a source for each tag value
                        for (var i = 0; i < tagValues.Count; i++)
                        {
                            string tagValue = tagValues[i];
                            xpSources.Break[tagValue] = source.Value;
                        }
                        break;
                }
            }
            //  Otherwise, use the source as-is.
            else
            {
                xpSources.Break[source.Key] = source.Value;
            }
        }
        
        var skill = new Skill(id, localizedName, localizedCategory, icon, assetInfo.MaxLevel, xpSources, assetInfo.Levels);
        return Result<Skill>.FromSuccess(skill);
    }
}