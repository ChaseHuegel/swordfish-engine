using System.Text;
using WaywardBeyond.AssetTools.Validation;
using WaywardBeyond.AssetTools.Workspace;
using Xunit;

namespace WaywardBeyond.AssetTools.Tests.Validation;

/// <summary>
///     Verifies <c>[Bricks.Entity.Light]</c> and <c>[[Bricks.Entity.SlotGroups]]</c> validation and inbound reference indexing.
/// </summary>
public sealed class BrickEntityValidationTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temp workspace directory for brick-entity validation tests.
    /// </summary>
    public BrickEntityValidationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-brick-entity-val-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ValidatesEntityLightAndSlotGroupsAndIndexesInboundSlotGroupRefs()
    {
        var packDir = Path.Combine(_tempRoot, "pack");
        Directory.CreateDirectory(Path.Combine(packDir, "assets", "bricks"));
        Directory.CreateDirectory(Path.Combine(packDir, "assets", "inventory"));
        Directory.CreateDirectory(Path.Combine(packDir, "assets", "textures", "block"));
        File.WriteAllText(Path.Combine(packDir, "pack.toml"), "ID = \"test\"\nName = \"Test\"\nVersion = \"1.0.0\"\n", Encoding.UTF8);
        File.WriteAllBytes(Path.Combine(packDir, "assets", "textures", "block", "panel.png"), [0x89, 0x50, 0x4E, 0x47]);

        File.WriteAllText(
            Path.Combine(packDir, "assets", "inventory", "slot_groups.toml"),
            """
            [[SlotGroups]]
            ID = "wb:cargo"
            Capacity = 27
            """,
            Encoding.UTF8);

        File.WriteAllText(
            Path.Combine(packDir, "assets", "bricks", "valid_brick.toml"),
            """
            [[Bricks]]
            ID = "test:valid_beacon"
            Shape = "block"
            [Bricks.Textures]
            Default = ["panel"]
            [Bricks.Statistics]
            "wb:power_kw" = -5.0
            [Bricks.Entity.Light]
            Radius = 4.0
            Size = 0.5
            Color = "#44AAFF"
            Intensity = 2.0
            CastShadows = false
            Offset = { X = 0.0, Y = 0.5, Z = 0.0 }
            [[Bricks.Entity.SlotGroups]]
            ID = "wb:cargo"
            Capacity = 12
            """,
            Encoding.UTF8);

        File.WriteAllText(
            Path.Combine(packDir, "assets", "bricks", "broken_brick.toml"),
            """
            [[Bricks]]
            ID = "test:broken_beacon"
            Shape = "block"
            [Bricks.Textures]
            Default = ["panel"]
            [Bricks.Entity.Light]
            Radius = -2.0
            Size = 0.0
            Color = "not-a-hex"
            Intensity = -1.0
            [[Bricks.Entity.SlotGroups]]
            ID = "wb:cargo"
            Capacity = -5
            [[Bricks.Entity.SlotGroups]]
            ID = "wb:cargo"
            [[Bricks.Entity.SlotGroups]]
            ID = "wb:unknown_group"
            """,
            Encoding.UTF8);

        var loaded = new WorkspaceLoader().LoadPack(packDir);
        Assert.True(loaded.Success, loaded.Message);
        var ws = loaded.Value;

        Assert.Contains(
            ws.GetInboundReferences("wb:cargo"),
            static r => r.SourceKind == AssetKind.Brick && r.SourceAssetId == "test:valid_beacon");

        var report = new WorkspaceValidator().Validate(ws);
        var findings = report.Findings;

        Assert.DoesNotContain(findings, static f => f.FilePath.EndsWith("valid_brick.toml", StringComparison.OrdinalIgnoreCase) && f.Severity == ValidationSeverity.Broken);

        var brokenFindings = findings
            .Where(static f => f.FilePath.EndsWith("broken_brick.toml", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Contains(brokenFindings, static f => f.Code == "COLOR001" && f.Field == "Entity.Light.Color");
        Assert.Contains(brokenFindings, static f => f.Code == "BRICK001" && f.Field == "Entity.Light.Radius");
        Assert.Contains(brokenFindings, static f => f.Code == "BRICK001" && f.Field == "Entity.Light.Size");
        Assert.Contains(brokenFindings, static f => f.Code == "BRICK001" && f.Field == "Entity.Light.Intensity");
        Assert.Contains(brokenFindings, static f => f.Code == "BRICK002" && f.Message.Contains("negative capacity", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(brokenFindings, static f => f.Code == "BRICK002" && f.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(brokenFindings, static f => f.Code == "BRICK002" && f.Message.Contains("wb:unknown_group", StringComparison.Ordinal));
    }
}
