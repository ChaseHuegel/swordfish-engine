using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Shoal.CommandLine;
using Shoal.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.Items;

/// <summary>Resolves and caches inventory icon materials, auto-rendering brick icons on startup.</summary>
internal sealed class ItemIconDatabase : IAutoActivate, IDisposable
{
    private const string UNKNOWN_ICON = "items/unknown.png";
    internal const string DUMP_ICONS_CLI_KEY = "dump-brick-icons";
    private const string DEFAULT_DUMP_DIRECTORY = "debug/icons";

    private readonly ILogger<ItemIconDatabase> _logger;
    private readonly IAssetDatabase<Item> _items;
    private readonly IAssetDatabase<Texture> _textures;
    private readonly Shader? _ownedShader;
    private readonly Material _unknownIcon;
    private readonly Dictionary<string, Material> _icons = [];
    private readonly Dictionary<string, Material> _brickIcons = new(StringComparer.Ordinal);
    private readonly List<Texture> _ownedTextures = [];
    private readonly List<Material> _ownedMaterials = [];

    /// <summary>Creates the icon cache and pre-bakes isometric icons for all registered bricks.</summary>
    public ItemIconDatabase(
        in ILogger<ItemIconDatabase> logger,
        in IFileParseService fileParseService,
        in IAssetDatabase<Texture> textures,
        in IAssetDatabase<Item> items,
        in IBrickDatabase bricks,
        in BrickIconRenderer brickIconRenderer,
        in CommandLineArgs commandLineArgs
    ) {
        _logger = logger;
        _items = items;
        _textures = textures;
        _ownedShader = fileParseService.Parse<Shader>(AssetPaths.Shaders.At("ui_reef_textured.glsl"));
        _unknownIcon = CreateUnknownMaterial(_ownedShader);

        BakeBrickIcons(bricks, brickIconRenderer, commandLineArgs);
    }

    /// <summary>Creates the icon cache without brick baking for UI tests.</summary>
    internal ItemIconDatabase(
        in ILogger<ItemIconDatabase> logger,
        in IFileParseService fileParseService,
        in IAssetDatabase<Texture> textures,
        in IAssetDatabase<Item> items
    ) {
        _logger = logger;
        _items = items;
        _textures = textures;
        _ownedShader = fileParseService.Parse<Shader>(AssetPaths.Shaders.At("ui_reef_textured.glsl"));
        _unknownIcon = CreateUnknownMaterial(_ownedShader);
    }

    /// <summary>Creates the icon cache with a caller-supplied UI shader and brick renderer for tests.</summary>
    internal ItemIconDatabase(
        in ILogger<ItemIconDatabase> logger,
        in Shader uiShader,
        in IAssetDatabase<Texture> textures,
        in IAssetDatabase<Item> items,
        in IBrickDatabase bricks,
        in BrickIconRenderer brickIconRenderer,
        in CommandLineArgs commandLineArgs
    ) {
        _logger = logger;
        _items = items;
        _textures = textures;
        _unknownIcon = CreateUnknownMaterial(uiShader);

        BakeBrickIcons(bricks, brickIconRenderer, commandLineArgs);
    }

    /// <summary>Returns the icon for an item id, falling back to the unknown icon.</summary>
    public Material Get(string itemID)
    {
        Result<Item> itemResult = _items.Get(itemID);
        if (!itemResult.Success)
        {
            return _unknownIcon;
        }

        Item item = itemResult.Value;
        string? iconID = item.IconID;
        if (iconID != null)
        {
            return GetOrLoadTextureIcon(iconID, itemID);
        }

        if (item.Placeable?.Type == PlaceableType.Brick
            && _brickIcons.TryGetValue(item.Placeable.Value.ID, out Material? brickIcon))
        {
            return brickIcon;
        }

        return _unknownIcon;
    }

    private Material CreateUnknownMaterial(Shader uiShader)
    {
        Result<Texture> unknownResult = _textures.Get(UNKNOWN_ICON);
        if (unknownResult.Success)
        {
            return new Material(uiShader, unknownResult.Value);
        }

        _logger.LogError(unknownResult, "Failed to load fallback icon \"{icon}\".", UNKNOWN_ICON);
        var fallback = new Texture("items/unknown_fallback", new byte[4], 1, 1, mipmaps: false);
        _ownedTextures.Add(fallback);
        return new Material(uiShader, fallback);
    }

    private Material GetOrLoadTextureIcon(string iconID, string itemID)
    {
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
        _ownedMaterials.Add(icon);
        return icon;
    }

    private void BakeBrickIcons(IBrickDatabase bricks, BrickIconRenderer brickIconRenderer, CommandLineArgs commandLineArgs)
    {
        List<Brick> allBricks = bricks.Get(_ => true);
        if (allBricks.Count == 0)
        {
            return;
        }

        Dictionary<string, Texture> rendered = brickIconRenderer.RenderBricks(allBricks);
        foreach ((string brickID, Texture texture) in rendered)
        {
            var material = new Material(_unknownIcon.Shader, texture);
            _brickIcons[brickID] = material;
            _ownedTextures.Add(texture);
            _ownedMaterials.Add(material);
        }

        string? dumpDirectory = GetDumpDirectory(commandLineArgs);
        if (dumpDirectory != null)
        {
            DumpIcons(rendered, dumpDirectory);
        }
    }

    /// <summary>Returns the CLI dump output directory, or null when brick icon dumping is not enabled.</summary>
    internal static string? GetDumpDirectory(CommandLineArgs commandLineArgs)
    {
        if (commandLineArgs.TryGetValue(DUMP_ICONS_CLI_KEY, out string? explicitDir) && !string.IsNullOrWhiteSpace(explicitDir))
        {
            return explicitDir;
        }

        return commandLineArgs.GetFlag(DUMP_ICONS_CLI_KEY) ? DEFAULT_DUMP_DIRECTORY : null;
    }

    private void DumpIcons(Dictionary<string, Texture> rendered, string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            foreach ((string brickID, Texture texture) in rendered)
            {
                string fileName = SanitizeFileName(brickID) + ".png";
                string outputPath = Path.Combine(directory, fileName);
                using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(texture.Pixels, texture.Width, texture.Height);
                image.Mutate(context => context.Flip(FlipMode.Vertical));
                image.SaveAsPng(outputPath);
            }

            _logger.LogInformation("Dumped {count} rendered brick icons to \"{directory}\".", rendered.Count, Path.GetFullPath(directory));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dump rendered brick icons to \"{directory}\".", directory);
        }
    }

    /// <summary>Replaces path-unsafe characters in a brick id for PNG file names.</summary>
    internal static string SanitizeFileName(string brickID)
    {
        return brickID.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        for (var i = 0; i < _ownedMaterials.Count; i++)
        {
            _ownedMaterials[i].Dispose();
        }
        _ownedMaterials.Clear();

        for (var i = 0; i < _ownedTextures.Count; i++)
        {
            _ownedTextures[i].Dispose();
        }
        _ownedTextures.Clear();

        _unknownIcon.Dispose();
        _ownedShader?.Dispose();
    }
}
