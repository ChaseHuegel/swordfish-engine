using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.Skills;

/// <summary>
///     Provides access to skill information from virtual resources.
/// </summary>
internal sealed class SkillDatabase : VirtualAssetDatabase<SkillDefinitions, SkillDefinition, Skill>, IAutoActivate
{
    private readonly Shader _iconShader;
    private readonly Material _unknownIcon;
    private readonly IAssetDatabase<Texture> _textureDatabase;
    private readonly ILocalization _localization;
    private readonly Dictionary<string, Material> _icons = [];

    public SkillDatabase(
        in ILogger<SkillDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs,
        in IAssetDatabase<Texture> textureDatabase,
        in ILocalization localization
        )
        : base(logger, fileParseService, vfs)
    {
        _textureDatabase = textureDatabase;
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
        
        var skill = new Skill(id, localizedName, localizedCategory, icon, assetInfo.MaxLevel, assetInfo.Sources, assetInfo.Levels);
        return Result<Skill>.FromSuccess(skill);
    }
}