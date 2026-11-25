#nullable enable
using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Items;
using WaywardBeyond.Server.Systems;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Tests the shipped Blueprint Projector item definition, starter inventory, and localization keys.</summary>
public class ProjectorToolTests
{
    [Fact]
    public void ShippedProjectorItemLoadsWithBlueprintToolAndExpectedMetadata()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Items", "assets")));

        IFileParser[] parsers =
        [
            new TomlParser<ItemDefinitions>(),
            new LocalizedTagDefinitionParser(),
        ];
        var parser = new VirtualFileParseService(parsers, vfs);
        var items = new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);

        Result<Item> result = items.Get("wb:projector");
        Assert.True(result.Success, result.Message);

        Item projector = result.Value;
        Assert.Equal("item.base.projector", projector.Name);
        Assert.Equal("item.base.projector.description", projector.Description);
        Assert.Equal("items/projector.png", projector.IconID);
        Assert.NotNull(projector.ViewModel);
        Assert.Equal("items/projector.obj", projector.ViewModel.Value.Mesh);
        Assert.Equal("items/projector", projector.ViewModel.Value.Material);
        Assert.NotNull(projector.WorldModel);
        Assert.Equal("items/projector.obj", projector.WorldModel.Value.Mesh);
        Assert.Equal("items/projector", projector.WorldModel.Value.Material);
        Assert.Equal(1, projector.MaxStack);
        Assert.NotNull(projector.Tool);
        Assert.Equal(ToolType.Blueprint, projector.Tool.Value.Type);
        Assert.Null(projector.Placeable);
    }

    [Fact]
    public void StarterInventoryIncludesProjectorAlongsideCoreTools()
    {
        ItemData[] starter = PlayerInitialInventory.StarterInventory();

        int projectorIndex = Array.FindIndex(starter, item => item.ID == "wb:projector");
        int drillIndex = Array.FindIndex(starter, item => item.ID == "wb:drill");

        Assert.True(projectorIndex >= 0);
        Assert.True(drillIndex >= 0);
        Assert.True(projectorIndex > drillIndex);
        Assert.Equal(1, starter[projectorIndex].Count);
        Assert.Equal(1, starter[projectorIndex].MaxSize);
        Assert.Contains(starter, item => item.ID == "wb:rock");
        Assert.Contains(starter, item => item.ID == "wb:panel");
        Assert.Contains(starter, item => item.ID == "laser");
        Assert.Contains(starter, item => item.ID == "wb:sprayer");
    }

    [Fact]
    public void EnglishLocalizationDefinesAllProjectorRegionAndBlueprintKeys()
    {
        string itemToml = File.ReadAllText(Path.Combine("assets", "lang", "en", "item.toml"));
        AssertContainsKey(itemToml, "item.base.projector");
        AssertContainsKey(itemToml, "item.base.projector.description");

        string notificationToml = File.ReadAllText(Path.Combine("assets", "lang", "en", "notification.toml"));
        foreach (string key in new[]
        {
            "notification.region.creativeOnly",
            "notification.region.permissionDenied",
            "notification.region.invalidBounds",
            "notification.region.invalidTarget",
            "notification.region.outOfReach",
            "notification.region.empty",
            "notification.region.noClipboard",
            "notification.region.differentStructure",
            "notification.region.copied",
            "notification.region.cut",
            "notification.region.pasted",
            "notification.region.spawned",
            "notification.region.pasteSkippedUnknown",
            "notification.region.overwriteAirOn",
            "notification.region.overwriteAirOff",
            "notification.blueprint.saved",
            "notification.blueprint.loaded",
            "notification.blueprint.renamed",
            "notification.blueprint.deleted",
            "notification.blueprint.saveFailed",
            "notification.blueprint.loadFailed",
            "notification.blueprint.nameRequired",
        })
        {
            AssertContainsKey(notificationToml, key);
        }

        string uiToml = File.ReadAllText(Path.Combine("assets", "lang", "en", "ui.toml"));
        foreach (string key in new[]
        {
            "ui.label.item.kind.blueprintTool",
            "ui.label.region.select",
            "ui.label.region.copy",
            "ui.label.region.cut",
            "ui.label.region.paste",
            "ui.label.region.rotate",
            "ui.label.region.mirror",
            "ui.label.region.overwriteAir",
            "ui.label.region.blueprints",
            "ui.blueprint.title",
            "ui.blueprint.close",
            "ui.blueprint.search.placeholder",
            "ui.blueprint.save.section",
            "ui.blueprint.save.placeholder",
            "ui.blueprint.save.button",
            "ui.blueprint.empty",
            "ui.blueprint.load",
            "ui.blueprint.rename",
            "ui.blueprint.delete",
            "ui.blueprint.saveRename",
            "ui.blueprint.cancel",
            "ui.blueprint.confirmDelete",
            "ui.blueprint.bricksSuffix",
            "ui.blueprint.builtIn",
            "ui.blueprint.hint",
            "ui.blueprint.error.nameRequired",
            "ui.blueprint.error.saveFailed",
            "ui.blueprint.error.loadFailed",
            "ui.hint.region.cornerA",
            "ui.hint.region.cornerB",
            "ui.hint.region.copy",
            "ui.hint.region.cut",
            "ui.hint.region.pasteMode",
            "ui.hint.region.paste",
            "ui.hint.region.cancelPaste",
            "ui.hint.region.rotate",
            "ui.hint.region.mirror",
            "ui.hint.region.overwriteAir",
            "ui.hint.region.distance",
            "ui.hint.region.embed",
            "ui.hint.region.radial",
            "ui.hint.region.library",
        })
        {
            AssertContainsKey(uiToml, key);
        }
    }

    private static void AssertContainsKey(string tomlContent, string key)
    {
        Assert.Contains($"\"{key}\" =", tomlContent, StringComparison.Ordinal);
    }
}
