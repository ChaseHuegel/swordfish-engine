using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.Items;

/// <summary>Resolves and caches the inventory icon material for an item's texture id.</summary>
internal sealed class ItemIconDatabase
{
    private const string UNKNOWN_ICON = "items/unknown.png";

    private readonly ILogger<ItemIconDatabase> _logger;
    private readonly IAssetDatabase<Item> _items;
    private readonly IAssetDatabase<Texture> _textures;
    private readonly Material _unknownIcon;
    private readonly Dictionary<string, Material> _icons = [];

    /// <summary>Creates the icon cache over the shared item database and the texture store.</summary>
    public ItemIconDatabase(
        in ILogger<ItemIconDatabase> logger,
        in IFileParseService fileParseService,
        in IAssetDatabase<Texture> textures,
        in IAssetDatabase<Item> items
    ) {
        _logger = logger;
        _items = items;
        _textures = textures;
        _unknownIcon = new Material(
            fileParseService.Parse<Shader>(AssetPaths.Shaders.At("ui_reef_textured.glsl")),
            textures.Get(UNKNOWN_ICON));
    }

    /// <summary>Returns the icon for an item id, falling back to the unknown icon.</summary>
    public Material Get(string itemID)
    {
        Result<Item> itemResult = _items.Get(itemID);
        if (!itemResult.Success)
        {
            return _unknownIcon;
        }

        string? iconID = itemResult.Value.IconID;
        if (iconID == null)
        {
            return _unknownIcon;
        }

        if (_icons.TryGetValue(iconID, out Material? icon))
        {
            return icon;
        }

        Result<Texture> textureResult = _textures.Get(iconID);
        if (!textureResult.Success)
        {
            _logger.LogError(textureResult, "Failed to get the icon \"{icon}\" for item \"{item}\".", iconID, itemID);
            _icons[iconID] = _unknownIcon;
            return _unknownIcon;
        }

        icon = new Material(_unknownIcon.Shader, textureResult.Value);
        _icons[iconID] = icon;
        return icon;
    }
}
