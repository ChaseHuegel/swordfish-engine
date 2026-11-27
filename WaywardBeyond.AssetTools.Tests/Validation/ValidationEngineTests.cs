using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Validation;
using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Tests.Validation;

/// <summary>
///     Verifies every consumer-aware validation rule, severity level, and bulk action.
/// </summary>
public sealed class ValidationEngineTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary workspace directory for validation tests.
    /// </summary>
    public ValidationEngineTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-val-tests-{Guid.NewGuid():N}");
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
    public void StrictEnumWrongGrammarsConnectedLayersAndSoundsAreValidated()
    {
        string pack = CreateMinimalPack("rules-pack");
        string assets = Path.Combine(pack, "assets");

        //  Add valid albedo layers for connected test, missing layer 14.
        string blockTex = Path.Combine(assets, "textures", "block");
        Directory.CreateDirectory(blockTex);
        for (int i = 0; i < 16; i++)
        {
            if (i == 14)
            {
                continue;
            }

            File.WriteAllBytes(Path.Combine(blockTex, $"panel.{i}.png"), [1, 2, 3]);
        }

        File.WriteAllBytes(Path.Combine(blockTex, "core.png"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(blockTex, "core.n.png"), [1, 2, 3]);

        //  Brick with invalid enum, invalid color, invalid collider, connected texture missing layer 14, wrong sound grammar, and PBR suffix layer.
        WriteFile(
            Path.Combine(assets, "bricks", "bad.toml"),
            "[[Bricks]]\n" +
            "ID    = \"wb:bad\"\n" +
            "Shape = \"InvalidShapeEnum\"\n" +
            "Color = \"not-hex\"\n" +
            "[Bricks.Sounds]\n" +
            "Place = [\"sounds/place/metal.wav\"]\n" +
            "Break = [\"missing_sound_tag\"]\n" +
            "[Bricks.Textures]\n" +
            "Connected = true\n" +
            "Default   = [\"panel.0\", \"core.n\"]\n" +
            "[[Bricks.Colliders]]\n" +
            "Type   = \"sphere\"\n" +
            "Radius = -2.0\n");

        var loader = new WorkspaceLoader();
        WorkspaceIndex ws = loader.LoadPack(pack).Value;
        ValidationReport report = new WorkspaceValidator().Validate(ws);

        Assert.True(report.BlocksExport);
        Assert.Contains(report.Findings, static f => f.Code == "ENUM001" && f.Severity == ValidationSeverity.Broken && f.Field == "Shape");
        Assert.Contains(report.Findings, static f => f.Code == "COLOR001" && f.Severity == ValidationSeverity.Broken);
        Assert.Contains(report.Findings, static f => f.Code == "COLLIDER001" && f.Severity == ValidationSeverity.Broken);
        Assert.Contains(report.Findings, static f => f.Code == "TEX002" && f.Message.Contains("panel.14", StringComparison.Ordinal));
        Assert.Contains(report.Findings, static f => f.Code == "REF002" && f.Field.StartsWith("Textures.Default", StringComparison.Ordinal));
        Assert.Contains(report.Findings, static f => f.Code == "REF002" && f.Field.StartsWith("Sounds.Place", StringComparison.Ordinal));
        Assert.Contains(report.Findings, static f => f.Code == "REF001" && f.Field.StartsWith("Sounds.Break", StringComparison.Ordinal));
    }

    [Fact]
    public void PaintSkillLocalizationAndInformationalFieldsRespectConsumerSeverity()
    {
        string pack = CreateMinimalPack("consumer-pack");
        string assets = Path.Combine(pack, "assets");

        //  Add an unused localization key — must NEVER be flagged!
        WriteFile(
            Path.Combine(assets, "lang", "en", "item.toml"),
            "\"Language\" = \"en\"\n\n[Translations]\n\"unused.from.csharp\" = \"Used in C#\"\n\"item.ok.name\" = \"Valid Item\"\n");
        WriteFile(
            Path.Combine(assets, "lang", "en", "dup.toml"),
            "\"Language\" = \"en\"\n\n[Translations]\n\"item.ok.name\" = \"Duplicate Key\"\n");

        //  Item with unread WorldModel broken refs (must be Informational, not Broken!) and valid Name.
        WriteFile(
            Path.Combine(assets, "items", "tool.toml"),
            "[[Items]]\n" +
            "ID   = \"wb:tool\"\n" +
            "Name = \"item.ok.name\"\n" +
            "[Items.WorldModel]\n" +
            "Mesh     = \"missing/world.obj\"\n" +
            "Material = \"missing/mat.toml\"\n");

        //  Paints: one with both Textures and Color, one with neither.
        WriteFile(
            Path.Combine(assets, "paints", "bad_paints.toml"),
            "[[Paints]]\n" +
            "ID       = \"wb:both\"\n" +
            "Color    = \"#ffffff\"\n" +
            "Textures = [\"core\"]\n\n" +
            "[[Paints]]\n" +
            "ID = \"wb:neither\"\n");

        //  Skills: duplicate ID, missing Levels, unread Category/Icon missing (must be Informational!).
        WriteFile(
            Path.Combine(assets, "skills", "s1.toml"),
            "[[Skills]]\n" +
            "ID       = \"mining\"\n" +
            "Name     = \"item.ok.name\"\n" +
            "Category = \"skill.cat.missing\"\n" +
            "Icon     = \"missing/icon.png\"\n" +
            "[Skills.Levels]\n" +
            "0 = 50\n");
        WriteFile(
            Path.Combine(assets, "skills", "s2.toml"),
            "[[Skills]]\n" +
            "ID = \"mining\"\n");

        var loader = new WorkspaceLoader();
        WorkspaceIndex ws = loader.LoadPack(pack).Value;
        ValidationReport report = new WorkspaceValidator().Validate(ws);

        Assert.DoesNotContain(report.Findings, static f => f.Message.Contains("unused.from.csharp", StringComparison.Ordinal));
        Assert.Contains(report.Findings, static f => f.Code == "LOC002" && f.Severity == ValidationSeverity.Broken);
        Assert.Equal(2, report.Findings.Count(static f => f.Code == "PAINT001"));
        Assert.Contains(report.Findings, static f => f.Code == "ID003" && f.Severity == ValidationSeverity.Broken);
        Assert.Contains(report.Findings, static f => f.Code == "SKILL001");
        Assert.Contains(report.Findings, static f => f.Code == "SKILL002");

        Assert.Contains(report.Findings, static f => f.Field == "WorldModel.Mesh" && f.Severity == ValidationSeverity.Informational);
        Assert.Contains(report.Findings, static f => f.Field == "WorldModel.Material" && f.Severity == ValidationSeverity.Informational);
        Assert.Contains(report.Findings, static f => f.Field == "Category" && f.Severity == ValidationSeverity.Informational);
        Assert.Contains(report.Findings, static f => f.Field == "Icon" && f.Severity == ValidationSeverity.Informational);
        Assert.DoesNotContain(report.Findings, static f => f.Field.StartsWith("WorldModel.", StringComparison.Ordinal) && f.Severity == ValidationSeverity.Broken);
    }

    [Fact]
    public void CaseMismatchBomCrlfAndFixCaseBulkActionWorkCleanly()
    {
        string pack = CreateMinimalPack("case-pack");
        string assets = Path.Combine(pack, "assets");
        string texPath = Path.Combine(assets, "textures", "items", "Drill.png");
        Directory.CreateDirectory(Path.GetDirectoryName(texPath)!);
        File.WriteAllBytes(texPath, [1, 2, 3]);

        WriteFile(
            Path.Combine(assets, "lang", "en", "item.toml"),
            "\"Language\" = \"en\"\n\n[Translations]\n\"item.drill\" = \"Drill\"\n");

        string itemPath = Path.Combine(assets, "items", "drill.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(itemPath)!);
        byte[] bom = Encoding.UTF8.GetPreamble();
        byte[] body = Encoding.UTF8.GetBytes("[[Items]]\r\nID   = \"wb:drill\"\r\nName = \"item.drill\"\r\nIcon = \"items/drill.png\"\r\n");
        byte[] combined = new byte[bom.Length + body.Length];
        Buffer.BlockCopy(bom, 0, combined, 0, bom.Length);
        Buffer.BlockCopy(body, 0, combined, bom.Length, body.Length);
        File.WriteAllBytes(itemPath, combined);

        var loader = new WorkspaceLoader();
        var validator = new WorkspaceValidator();
        WorkspaceIndex ws = loader.LoadPack(pack).Value;
        ValidationReport before = validator.Validate(ws);

        Assert.Contains(before.Findings, static f => f.Code == "FMT001" && f.Severity == ValidationSeverity.CrossPlatform);
        Assert.Contains(before.Findings, static f => f.Code == "FMT002" && f.Severity == ValidationSeverity.CrossPlatform);
        Assert.Contains(before.Findings, static f => f.Code == "PATH001" && f.SuggestedFixValue == "items/Drill.png");

        string copied = ValidationBulkActions.CopyAll(before);
        Assert.Contains("[CrossPlatform]", copied, StringComparison.Ordinal);

        Result<int> fixedCount = ValidationBulkActions.FixCase(ws, before);
        Assert.True(fixedCount.Success);
        Assert.Equal(1, fixedCount.Value);
        Assert.Contains("Icon = \"items/Drill.png\"", File.ReadAllText(itemPath), StringComparison.Ordinal);

        Result<int> formattedCount = ValidationBulkActions.FormatFiles([itemPath]);
        Assert.True(formattedCount.Success);
        Assert.Equal(1, formattedCount.Value);

        Result<(WorkspaceIndex Workspace, ValidationReport Report)> revalidated =
            ValidationBulkActions.Revalidate(loader, validator, ws);
        Assert.True(revalidated.Success);
        Assert.Empty(revalidated.Value.Report.Findings);
    }

    [Fact]
    public void BrickSoundTagsItemToolEnumsAndDeterministicFixesValidateCleanly()
    {
        string pack = CreateMinimalPack("sound-enum-pack");
        string assets = Path.Combine(pack, "assets");

        WriteBytes(Path.Combine(assets, "audio", "sounds", "place", "metal.wav"), [1, 2, 3]);
        WriteBytes(Path.Combine(assets, "textures", "block", "hull.png"), [1, 2, 3]);
        WriteBytes(Path.Combine(assets, "shaders", "textured.glsl"), [1, 2, 3]);
        WriteFile(
            Path.Combine(assets, "materials", "items", "tool_mat.toml"),
            "Shader = \"textured\"\nTextures = [\"block/hull.png\"]\n");
        WriteFile(
            Path.Combine(assets, "lang", "tags", "sounds.toml"),
            "\"Language\" = \"\"\n\n[Tags]\n\"metal_place\" = [\n    \"sounds/place/metal.wav\",\n]\n");
        WriteFile(
            Path.Combine(assets, "lang", "en", "item.toml"),
            "\"Language\" = \"en\"\n\n[Translations]\n\"item.valid.name\" = \"Valid\"\n");

        //  Valid brick using invariant sound tag must produce zero sound findings.
        WriteFile(
            Path.Combine(assets, "bricks", "good.toml"),
            "[[Bricks]]\n" +
            "ID    = \"wb:good\"\n" +
            "Shape = \"block\"\n" +
            "[Bricks.Sounds]\n" +
            "Place = [\"metal_place\"]\n" +
            "[Bricks.Textures]\n" +
            "Default = [\"hull\"]\n");

        //  Brick with audio clip path (suggests unique sound tag) and case-mismatched sound tag + bad face path.
        WriteFile(
            Path.Combine(assets, "bricks", "sound_issues.toml"),
            "[[Bricks]]\n" +
            "ID    = \"wb:sound_issues\"\n" +
            "Shape = \"block\"\n" +
            "[Bricks.Sounds]\n" +
            "Place = [\"sounds/place/metal.wav\"]\n" +
            "Break = [\"Metal_Place\"]\n" +
            "[Bricks.Textures]\n" +
            "Default = [\"block/hull.png\"]\n");

        //  Valid items covering every real ToolType and PlaceableType value.
        WriteFile(
            Path.Combine(assets, "items", "valid_tools.toml"),
            "[[Items]]\nID = \"wb:t1\"\nName = \"item.valid.name\"\n[Items.Tool]\nType = \"Convert\"\n\n" +
            "[[Items]]\nID = \"wb:t2\"\nName = \"item.valid.name\"\n[Items.Tool]\nType = \"Mining\"\n\n" +
            "[[Items]]\nID = \"wb:t3\"\nName = \"item.valid.name\"\n[Items.Tool]\nType = \"Sprayer\"\n\n" +
            "[[Items]]\nID = \"wb:t4\"\nName = \"item.valid.name\"\n[Items.Tool]\nType = \"Drill\"\n\n" +
            "[[Items]]\nID = \"wb:t5\"\nName = \"item.valid.name\"\n[Items.Tool]\nType = \"Blueprint\"\n\n" +
            "[[Items]]\nID = \"wb:p1\"\nName = \"item.valid.name\"\n[Items.Placeable]\nType = \"Brick\"\nID = \"wb:good\"\n");

        //  Invalid ToolType values (including fake enum names and numeric strings) and invalid PlaceableType + .toml material suffix.
        WriteFile(
            Path.Combine(assets, "items", "bad_tools.toml"),
            "[[Items]]\nID = \"wb:bad_laser\"\nName = \"item.valid.name\"\n[Items.Tool]\nType = \"laser\"\n\n" +
            "[[Items]]\nID = \"wb:bad_projector\"\nName = \"item.valid.name\"\n[Items.Tool]\nType = \"projector\"\n\n" +
            "[[Items]]\nID = \"wb:bad_numeric\"\nName = \"item.valid.name\"\n[Items.Tool]\nType = \"999\"\n\n" +
            "[[Items]]\nID = \"wb:bad_placeable\"\nName = \"item.valid.name\"\n[Items.Placeable]\nType = \"Voxel\"\n" +
            "[Items.ViewModel]\nMaterial = \"items/tool_mat.toml\"\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        ValidationReport report = new WorkspaceValidator().Validate(ws);

        Assert.DoesNotContain(report.Findings, static f => f.FilePath.EndsWith("good.toml", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Findings, static f => f.FilePath.EndsWith("valid_tools.toml", StringComparison.Ordinal));

        Assert.Contains(report.Findings, static f =>
            f.Code == "REF002" &&
            f.Field == "Sounds.Place[0]" &&
            f.SuggestedFixValue == "metal_place");
        Assert.Contains(report.Findings, static f =>
            f.Code == "PATH001" &&
            f.Field == "Sounds.Break[0]" &&
            f.SuggestedFixValue == "metal_place");
        Assert.Contains(report.Findings, static f =>
            f.Code == "REF002" &&
            f.Field == "Textures.Default[0]" &&
            f.SuggestedFixValue == "hull");
        Assert.Contains(report.Findings, static f =>
            f.Code == "REF002" &&
            f.Field == "ViewModel.Material" &&
            f.SuggestedFixValue == "items/tool_mat");

        Assert.Equal(3, report.Findings.Count(static f => f.Code == "ENUM001" && f.Field == "Items.Tool.Type"));
        Assert.Equal(1, report.Findings.Count(static f => f.Code == "ENUM001" && f.Field == "Items.Placeable.Type"));
    }

    [Fact]
    public void ValidatesBlueprintPlaceableReferencesWithPath001AndRef001()
    {
        string pack = CreateMinimalPack("blueprint_placeable_pack");
        string assets = Path.Combine(pack, "assets");
        WriteFile(
            Path.Combine(assets, "lang", "en", "items.toml"),
            "\"Language\" = \"en\"\n[Translations]\n\"item.bp.name\" = \"Core\"\n");

        //  Write one bundled blueprint at assets/blueprints/builtin/core.wbp.
        WriteBytes(Path.Combine(assets, "blueprints", "builtin", "core.wbp"), [1, 2, 3, 4]);

        WriteFile(
            Path.Combine(assets, "items", "valid_bp.toml"),
            "[[Items]]\nID = \"wb:core_ok\"\nName = \"item.bp.name\"\n[Items.Placeable]\nType = \"blueprint\"\nID = \"builtin/core\"\n");

        WriteFile(
            Path.Combine(assets, "items", "case_bp.toml"),
            "[[Items]]\nID = \"wb:core_case\"\nName = \"item.bp.name\"\n[Items.Placeable]\nType = \"blueprint\"\nID = \"builtin/CORE\"\n");

        WriteFile(
            Path.Combine(assets, "items", "missing_bp.toml"),
            "[[Items]]\nID = \"wb:core_missing\"\nName = \"item.bp.name\"\n[Items.Placeable]\nType = \"blueprint\"\nID = \"builtin/cor\"\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        ValidationReport report = new WorkspaceValidator().Validate(ws);

        Assert.DoesNotContain(report.Findings, static f => f.FilePath.EndsWith("valid_bp.toml", StringComparison.Ordinal));

        Assert.Contains(report.Findings, static f =>
            f.Code == "PATH001" &&
            f.Field == "Placeable.ID" &&
            f.SuggestedFixValue == "builtin/core" &&
            f.IsSafeBulkFix);

        Assert.Contains(report.Findings, static f =>
            f.Code == "REF001" &&
            f.Field == "Placeable.ID" &&
            f.SuggestedFixValue == "builtin/core" &&
            !f.IsSafeBulkFix);
    }

    [Fact]
    public void ValidatesCraftAndProcessSkillXpSourcesCleanly()
    {
        string pack = CreateMinimalPack("skill_craft_pack");
        string assets = Path.Combine(pack, "assets");
        WriteFile(
            Path.Combine(assets, "lang", "en", "skills.toml"),
            "\"Language\" = \"en\"\n[Translations]\n\"skill.name.manufacturing\" = \"Manufacturing\"\n");
        WriteFile(
            Path.Combine(assets, "lang", "tags", "items.toml"),
            "\"Language\" = \"\"\n[Tags]\n\"item.placeable\" = [\"wb:panel\"]\n");
        WriteFile(
            Path.Combine(assets, "skills", "manufacturing.toml"),
            "[[Skills]]\n" +
            "ID = \"manufacturing\"\n" +
            "Name = \"skill.name.manufacturing\"\n" +
            "[Skills.Sources.Craft]\n" +
            "\"station:station.synthesizer\" = 5\n" +
            "\"recipe:wb:craft_core\" = 20\n" +
            "\"tag:item.placeable\" = 4\n" +
            "\"wb:panel\" = 8\n" +
            "[Skills.Sources.Process]\n" +
            "\"station:station.refinery\" = 3\n" +
            "\"tag:missing.item_tag\" = 2\n" +
            "[Skills.Levels]\n" +
            "1 = 100\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        ValidationReport report = new WorkspaceValidator().Validate(ws);

        Assert.DoesNotContain(report.Findings, static f => f.Code == "ENUM001");
        Assert.DoesNotContain(report.Findings, static f => f.Field.Contains("station:", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Findings, static f => f.Field.Contains("recipe:", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Findings, static f => f.Field.Contains("wb:panel", StringComparison.Ordinal));
        Assert.Contains(report.Findings, static f => f.Code == "REF001" && f.Field == "Skills.Sources.Process.tag:missing.item_tag");
    }

    [Fact]
    public void ValidatesBrickHardnessAndToolMiningSpeed()
    {
        string pack = CreateMinimalPack("mining_stats_pack");
        string assets = Path.Combine(pack, "assets");
        WriteFile(
            Path.Combine(assets, "lang", "en", "items.toml"),
            "\"Language\" = \"en\"\n[Translations]\n\"item.tool.name\" = \"Laser\"\n");
        WriteBytes(Path.Combine(assets, "textures", "block", "stone.png"), [1, 2, 3, 4]);

        WriteFile(
            Path.Combine(assets, "bricks", "valid_brick.toml"),
            "[[Bricks]]\nID = \"wb:stone\"\nShape = \"block\"\nHardness = 2.5\n[Bricks.Textures]\nDefault = [\"stone\"]\n");
        WriteFile(
            Path.Combine(assets, "bricks", "zero_brick.toml"),
            "[[Bricks]]\nID = \"wb:fragile\"\nShape = \"block\"\nHardness = 0.0\n[Bricks.Textures]\nDefault = [\"stone\"]\n");
        WriteFile(
            Path.Combine(assets, "bricks", "negative_brick.toml"),
            "[[Bricks]]\nID = \"wb:bad_hardness\"\nShape = \"block\"\nHardness = -0.5\n[Bricks.Textures]\nDefault = [\"stone\"]\n");

        WriteFile(
            Path.Combine(assets, "items", "valid_tool.toml"),
            "[[Items]]\nID = \"wb:fast_laser\"\nName = \"item.tool.name\"\n[Items.Tool]\nType = \"Mining\"\nMiningSpeed = 1.75\n");
        WriteFile(
            Path.Combine(assets, "items", "zero_tool.toml"),
            "[[Items]]\nID = \"wb:zero_laser\"\nName = \"item.tool.name\"\n[Items.Tool]\nType = \"Mining\"\nMiningSpeed = 0.0\n");
        WriteFile(
            Path.Combine(assets, "items", "negative_tool.toml"),
            "[[Items]]\nID = \"wb:neg_laser\"\nName = \"item.tool.name\"\n[Items.Tool]\nType = \"Mining\"\nMiningSpeed = -1.5\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        ValidationReport report = new WorkspaceValidator().Validate(ws);

        Assert.DoesNotContain(report.Findings, static f => f.FilePath.EndsWith("valid_brick.toml", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Findings, static f => f.FilePath.EndsWith("zero_brick.toml", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Findings, static f => f.FilePath.EndsWith("valid_tool.toml", StringComparison.Ordinal));

        Assert.Contains(report.Findings, static f =>
            f.Code == "BRICK001" &&
            f.Field == "Hardness" &&
            f.FilePath.EndsWith("negative_brick.toml", StringComparison.Ordinal));
        Assert.Contains(report.Findings, static f =>
            f.Code == "TOOL001" &&
            f.Field == "Tool.MiningSpeed" &&
            f.FilePath.EndsWith("zero_tool.toml", StringComparison.Ordinal));
        Assert.Contains(report.Findings, static f =>
            f.Code == "TOOL001" &&
            f.Field == "Tool.MiningSpeed" &&
            f.FilePath.EndsWith("negative_tool.toml", StringComparison.Ordinal));
    }

    private string CreateMinimalPack(string name)
    {
        string pack = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(Path.Combine(pack, "assets"));
        File.WriteAllText(Path.Combine(pack, "manifest.toml"), $"ID = \"{name}\"\nName = \"{name}\"\n");
        return pack;
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteBytes(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }
}
