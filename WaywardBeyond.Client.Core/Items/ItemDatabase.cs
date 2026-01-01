using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.Items;

/// <summary>
///     Provides access to item information from virtual resources.
/// </summary>
internal sealed class ItemDatabase : VirtualAssetDatabase<ItemDefinitions, ItemDefinition, Item>, IAutoActivate
{
    private readonly Shader _iconShader;
    private readonly Material _unknownIcon;
    private readonly IAssetDatabase<Texture> _textureDatabase;
    private readonly ILocalization _localization;
    private readonly Dictionary<string, Material> _icons = [];

    public ItemDatabase(
        in ILogger<ItemDatabase> logger,
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
        _unknownIcon = new Material(_iconShader, textureDatabase.Get("items/unknown.png"));
        Load();
    }
    
    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");
    
    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Root.At("items");
    
    /// <inheritdoc/>
    protected override IEnumerable<ItemDefinition> GetAssetInfo(PathInfo path, ItemDefinitions resource) => resource.Items;

    /// <inheritdoc/>
    protected override string GetAssetID(ItemDefinition assetInfo) => assetInfo.ID;
    
    /// <inheritdoc/>
    protected override Result<Item> LoadAsset(string id, ItemDefinition assetInfo)
    {
        //  Get an icon for the item
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
                Logger.LogError(textureResult, "Failed to get the icon \"{icon}\" for item \"{item}\".", assetInfo.Icon, id);
                icon = _unknownIcon;
            }
        }
        
        //  TODO #349 when allowing language to be changed at runtime, these assets need to be reinitialized
        //  Localize the display name if a translation exists
        string localizedName = _localization.GetString(assetInfo.Name) ?? assetInfo.Name;
        
        var item = new Item(id, localizedName, icon, assetInfo.MaxStack ?? 1, assetInfo.Placeable, assetInfo.Tool, assetInfo.ViewModel, assetInfo.WorldModel);
        return Result<Item>.FromSuccess(item);
    }
}