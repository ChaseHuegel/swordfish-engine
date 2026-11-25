using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Validation;
using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Tests.Validation;

/// <summary>
///     Verifies expanded automatic quick-fix generation, <see cref="ValidationBulkActions.ApplySingleFix"/>, and <see cref="ValidationBulkActions.FixAllSafeIssues"/>.
/// </summary>
public sealed class ValidationQuickFixTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary directory for validation quick-fix tests.
    /// </summary>
    public ValidationQuickFixTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-qfix-tests-{Guid.NewGuid():N}");
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
    public void ValidatorGeneratesQuickFixesAcrossGrammarCaseEnumColorRefLocIdSkillTagAndPaintRules()
    {
        string pack = CreateMinimalPack("all-rules-pack");
        string assets = Path.Combine(pack, "assets");

        WriteBytes(Path.Combine(assets, "textures", "items", "Drill.png"), [1, 2, 3]);
        WriteBytes(Path.Combine(assets, "textures", "block", "hull.png"), [1, 2, 3]);
        WriteBytes(Path.Combine(assets, "meshes", "items", "Drill.obj"), [1, 2, 3]);
        WriteBytes(Path.Combine(assets, "audio", "sounds", "place", "metal.wav"), [1, 2, 3]);
        WriteBytes(Path.Combine(assets, "shaders", "textured.glsl"), [1, 2, 3]);

        WriteFile(
            Path.Combine(assets, "materials", "items", "tool_mat.toml"),
            "Shader = \"/shaders/textured.glsl\"\nTextures = [\"textures\\\\items\\\\drill.png\"]\n");
        WriteFile(
            Path.Combine(assets, "materials", "items", "typo_mat.toml"),
            "Shader = \"texturd\"\nTextures = [\"items/Dril.png\"]\n");

        WriteFile(
            Path.Combine(assets, "lang", "en", "item.toml"),
            "\"Language\" = \"en\"\n\n[Translations]\n\"item.drill.name\" = \"Drill\"\n\"item.valid.name\" = \"Valid\"\n");

        WriteFile(
            Path.Combine(assets, "lang", "tags", "sounds.toml"),
            "\"Language\" = \"\"\n\n[Tags]\n\"metal_place\" = [\n    \"audio/sounds/place/metal.wav\",\n    \"sounds/place/metal.wav\",\n    \"sounds/place/metal.wav\",\n]\n");

        WriteFile(
            Path.Combine(assets, "lang", "tags", "bricks.toml"),
            "\"Language\" = \"\"\n\n[Tags]\n\"brick.solid\" = [\n    \"WB:CORE\",\n    \"core\",\n    \"wb:coer\",\n]\n");

        WriteFile(
            Path.Combine(assets, "bricks", "core.toml"),
            "[[Bricks]]\n" +
            "ID    = \"wb:core\"\n" +
            "Shape = \"block\"\n" +
            "Color = \"ff8800\"\n" +
            "[Bricks.Sounds]\n" +
            "Place = [\"audio\\\\sounds\\\\place\\\\metal.wav\"]\n" +
            "Break = [\"metal_plce\"]\n" +
            "[Bricks.Textures]\n" +
            "Default = [\"textures/block/hull.n.png\"]\n\n" +
            "[[Bricks]]\n" +
            "ID    = \"wb:core\"\n" +
            "Shape = \"Blcok\"\n" +
            "Color = \"#F80\"\n" +
            "Mesh  = \"meshes/items/drill\"\n\n" +
            "[[Bricks]]\n" +
            "ID    = \"wb:alpha\"\n" +
            "Shape = \"block\"\n" +
            "Color = \"#FF8800AA\"\n");

        WriteFile(
            Path.Combine(assets, "paints", "paints.toml"),
            "[[Paints]]\n" +
            "ID       = \"wb:both\"\n" +
            "Name     = \"item.valid.name\"\n" +
            "Color    = \"0x112233\"\n" +
            "Textures = [\"hul\"]\n");

        WriteFile(
            Path.Combine(assets, "items", "items.toml"),
            "[[Items]]\n" +
            "ID          = \"wb:drill\"\n" +
            "Name        = \"item/drill/name\"\n" +
            "Description = \"ITEM.DRILL.NAME\"\n" +
            "[Items.Placeable]\n" +
            "Type = \"Brick\"\n" +
            "ID   = \"wb:core\"\n" +
            "[Items.ViewModel]\n" +
            "Material = \"materials\\\\items\\\\tool_mat.toml\"\n\n" +
            "[[Items]]\n" +
            "ID   = \"wb:missing_loc\"\n" +
            "Name = \"item.brand_new.name\"\n" +
            "[Items.Placeable]\n" +
            "Type = \"Brick\"\n" +
            "ID   = \"WB:CORE\"\n");

        WriteFile(
            Path.Combine(assets, "skills", "skills.toml"),
            "[[Skills]]\n" +
            "ID   = \"mining\"\n" +
            "Name = \"item.valid.name\"\n" +
            "[Skills.Sources.Break]\n" +
            "\"WB:CORE\" = 5\n" +
            "\"tag:metal_plce\" = 2\n\n" +
            "[[Skills]]\n" +
            "ID   = \"mining\"\n" +
            "Name = \"item.valid.name\"\n" +
            "[Skills.Sources.Brak]\n" +
            "\"wb:core\" = 1\n" +
            "[Skills.Levels]\n" +
            "1 = 0\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        ValidationReport report = new WorkspaceValidator().Validate(ws);

        //  REF002 normalizations are safe bulk ReplaceValue fixes.
        Assert.Contains(report.Findings, static f => f.Code == "REF002" && f.Field == "Shader" && f.SuggestedFixValue == "textured" && f.FixKind == ValidationFixKind.ReplaceValue && f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF002" && f.Field == "Textures[0]" && f.SuggestedFixValue == "items/Drill.png" && f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF002" && f.Field == "Mesh" && f.SuggestedFixValue == "items/Drill.obj" && f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF002" && f.Field == "ViewModel.Material" && f.SuggestedFixValue == "items/tool_mat" && f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF002" && f.Field == "Sounds.Place[0]" && f.SuggestedFixValue == "metal_place" && f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF002" && f.Field == "Textures.Default[0]" && f.SuggestedFixValue == "hull" && f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF002" && f.Field == "Name" && f.SuggestedFixValue == "item.drill.name" && f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF002" && f.Field == "metal_place[0]" && f.SuggestedFixValue == "sounds/place/metal.wav" && f.IsSafeBulkFix);

        //  PATH001 covers localization, brick IDs, skill source keys, and tag entries.
        Assert.Contains(report.Findings, static f => f.Code == "PATH001" && f.Field == "Description" && f.SuggestedFixValue == "item.drill.name" && f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "PATH001" && f.Field == "Placeable.ID" && f.SuggestedFixValue == "wb:core" && f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "PATH001" && f.Field == "Skills.Sources.Break.WB:CORE" && f.SuggestedFixValue == "wb:core" && f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "PATH001" && f.Field == "brick.solid[0]" && f.SuggestedFixValue == "wb:core" && f.IsSafeBulkFix);

        //  COLOR001 distinguishes safe 3/6-digit normalizations from unsafe 8-digit alpha stripping.
        Assert.Contains(report.Findings, static f => f.Code == "COLOR001" && f.SuggestedFixValue == "#FF8800" && f.IsSafeBulkFix && f.Message.Contains("ff8800", StringComparison.Ordinal));
        Assert.Contains(report.Findings, static f => f.Code == "COLOR001" && f.SuggestedFixValue == "#FF8800" && f.IsSafeBulkFix && f.Message.Contains("#F80", StringComparison.Ordinal));
        Assert.Contains(report.Findings, static f => f.Code == "COLOR001" && f.SuggestedFixValue == "#112233" && f.IsSafeBulkFix && f.Message.Contains("0x112233", StringComparison.Ordinal));
        Assert.Contains(report.Findings, static f => f.Code == "COLOR001" && f.SuggestedFixValue == "#FF8800" && !f.IsSafeBulkFix && f.Message.Contains("#FF8800AA", StringComparison.Ordinal));

        //  ENUM001, REF001, LOC001, ID002/ID003, SKILL001, SKILL002, TAG001, PAINT001.
        Assert.Contains(report.Findings, static f => f.Code == "ENUM001" && f.Field == "Shape" && f.SuggestedFixValue == "Block" && !f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "ENUM001" && f.Field == "Skills.Sources.Brak" && f.SuggestedFixValue == "Break" && !f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF001" && f.Field == "Shader" && f.SuggestedFixValue == "textured" && !f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF001" && f.Field == "Textures[0]" && f.SuggestedFixValue == "items/Drill.png" && !f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF001" && f.Field == "Sounds.Break[0]" && f.SuggestedFixValue == "metal_place" && !f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF001" && f.Field == "Textures[0]" && f.SuggestedFixValue == "hull" && !f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF001" && f.Field == "brick.solid[1]" && f.SuggestedFixValue == "wb:core" && !f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF001" && f.Field == "brick.solid[2]" && f.SuggestedFixValue == "wb:core" && !f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF001" && f.Field == "Skills.Sources.Break.tag:metal_plce" && f.SuggestedFixValue == "tag:metal_place" && !f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "LOC001" && f.FixKind == ValidationFixKind.CreateLocalizationKey && f.SuggestedFixValue == "item.brand_new.name");
        Assert.Contains(report.Findings, static f => f.Code == "ID002" && f.FixKind == ValidationFixKind.ReplaceValue && f.SuggestedFixValue == "wb:core_2");
        Assert.Contains(report.Findings, static f => f.Code == "ID003" && f.FixKind == ValidationFixKind.ReplaceValue && f.SuggestedFixValue == "mining_2");
        Assert.Contains(report.Findings, static f => f.Code == "SKILL001" && f.FixKind == ValidationFixKind.InsertDefaultSkillLevels && f.FieldSpan.HasValue);
        Assert.Contains(report.Findings, static f => f.Code == "SKILL002" && f.FixKind == ValidationFixKind.ReplaceValue && f.SuggestedFixValue == "1");
        Assert.Contains(report.Findings, static f => f.Code == "TAG001" && f.FixKind == ValidationFixKind.RemoveArrayElement && f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "PAINT001" && f.FixKind == ValidationFixKind.RemoveField && f.FieldSpan.HasValue && !f.IsSafeBulkFix);
    }

    [Fact]
    public void ApplySingleFixHandlesEveryFixKindIncludingIntegersTablesLocAndReadOnlySkipping()
    {
        string installDir = Path.Combine(_tempRoot, "game-install");
        string baseDir = Path.Combine(installDir, "modules", "base");
        string packDir = Path.Combine(_tempRoot, "single-fix-pack");

        WriteFile(Path.Combine(installDir, "config", "modules.toml"), "LoadOrder = [\"base\"]\n");
        WriteFile(Path.Combine(baseDir, "manifest.toml"), "ID = \"base\"\nName = \"Base\"\n");
        WriteFile(
            Path.Combine(baseDir, "assets", "bricks", "base_bad.toml"),
            "[[Bricks]]\r\nID = \"wb:base_bad\"\r\nColor = \"ff0000\"\r\n");

        WriteFile(Path.Combine(packDir, "manifest.toml"), "ID = \"pack\"\nName = \"Pack\"\n");
        string assets = Path.Combine(packDir, "assets");
        WriteBytes(Path.Combine(assets, "textures", "block", "hull.png"), [1, 2, 3]);

        string skillsPath = Path.Combine(assets, "skills", "skills.toml");
        WriteFile(
            skillsPath,
            "[[Skills]]\n" +
            "ID = \"mining\"\n" +
            "[Skills.Sources.Brak]\n" +
            "\"wb:base_bad\" = 5\n\n" +
            "[[Skills]]\n" +
            "ID = \"mining\"\n" +
            "[Skills.Levels]\n" +
            "1 = -5\n");

        string paintPath = Path.Combine(assets, "paints", "paint.toml");
        WriteFile(
            paintPath,
            "[[Paints]]\n" +
            "ID       = \"wb:both\"\n" +
            "Color    = \"#ffffff\"\n" +
            "Textures = [\"hull\"]\n");

        string itemPath = Path.Combine(assets, "items", "item.toml");
        WriteFile(
            itemPath,
            "[[Items]]\n" +
            "ID   = \"wb:laser\"\n" +
            "Name = \"item.laser.name\"\n");

        var loader = new WorkspaceLoader();
        var validator = new WorkspaceValidator();

        //  1. Read-only finding is skipped cleanly with Result.Success == true and Value == false.
        WorkspaceIndex ws0 = loader.LoadPack(packDir, installDir).Value;
        ValidationReport rep0 = validator.Validate(ws0);
        ValidationFinding readOnlyCrlf = rep0.Findings.First(f => f.Code == "FMT002" && f.RootId == "base");
        Result<bool> skippedRo = ValidationBulkActions.ApplySingleFix(ws0, readOnlyCrlf);
        Assert.True(skippedRo.Success);
        Assert.False(skippedRo.Value);

        //  2. Apply SKILL001 (InsertDefaultSkillLevels) under first [[Skills]] block.
        ValidationFinding skill001 = rep0.Findings.First(f => f.Code == "SKILL001");
        Result<bool> appliedSkill001 = ValidationBulkActions.ApplySingleFix(ws0, skill001);
        Assert.True(appliedSkill001.Success, appliedSkill001.Message);
        Assert.True(appliedSkill001.Value);
        Assert.Contains("[Skills.Levels]\n1 = 100\n", File.ReadAllText(skillsPath), StringComparison.Ordinal);

        //  3. Reload and apply SKILL002 (bare integer 1, NOT quoted "1"), ENUM001 table header ([Skills.Sources.Break]), and ID003.
        WorkspaceIndex ws1 = loader.LoadPack(packDir, installDir).Value;
        ValidationReport rep1 = validator.Validate(ws1);

        ValidationFinding skill002 = rep1.Findings.First(f => f.Code == "SKILL002");
        Assert.True(ValidationBulkActions.ApplySingleFix(ws1, skill002).Value);
        Assert.Contains("1 = 1\n", File.ReadAllText(skillsPath), StringComparison.Ordinal);
        Assert.DoesNotContain("1 = \"1\"", File.ReadAllText(skillsPath), StringComparison.Ordinal);

        ws1 = loader.LoadPack(packDir, installDir).Value;
        rep1 = validator.Validate(ws1);
        ValidationFinding enumHeader = rep1.Findings.First(f => f.Code == "ENUM001" && f.Field == "Skills.Sources.Brak");
        Assert.True(ValidationBulkActions.ApplySingleFix(ws1, enumHeader).Value);
        Assert.Contains("[Skills.Sources.Break]", File.ReadAllText(skillsPath), StringComparison.Ordinal);

        ws1 = loader.LoadPack(packDir, installDir).Value;
        rep1 = validator.Validate(ws1);
        ValidationFinding id003 = rep1.Findings.First(f => f.Code == "ID003");
        Assert.True(ValidationBulkActions.ApplySingleFix(ws1, id003).Value);
        Assert.Contains("ID = \"mining_2\"", File.ReadAllText(skillsPath), StringComparison.Ordinal);

        //  4. Apply PAINT001 (RemoveField removes conflicting Color line).
        ValidationFinding paint001 = rep1.Findings.First(f => f.Code == "PAINT001");
        Assert.True(ValidationBulkActions.ApplySingleFix(ws1, paint001).Value);
        string paintAfter = File.ReadAllText(paintPath);
        Assert.DoesNotContain("Color", paintAfter, StringComparison.Ordinal);
        Assert.Contains("Textures = [\"hull\"]", paintAfter, StringComparison.Ordinal);

        //  5. Apply LOC001 (CreateLocalizationKey creates lang/en/item.toml with item.laser.name).
        ValidationFinding loc001 = rep1.Findings.First(f => f.Code == "LOC001");
        Assert.True(ValidationBulkActions.ApplySingleFix(ws1, loc001).Value);
        string langFile = Path.Combine(assets, "lang", "en", "item.toml");
        Assert.True(File.Exists(langFile));
        Assert.Contains("\"item.laser.name\" = \"Laser\"", File.ReadAllText(langFile), StringComparison.Ordinal);

        //  6. Revalidate: zero Skill/Paint/Loc findings remain in pack!
        WorkspaceIndex wsFinal = loader.LoadPack(packDir, installDir).Value;
        ValidationReport repFinal = validator.Validate(wsFinal);
        Assert.DoesNotContain(repFinal.Findings, f => f.RootId == "pack" && (f.Code == "SKILL001" || f.Code == "SKILL002" || f.Code == "ID003" || f.Code == "PAINT001" || f.Code == "LOC001"));
    }

    [Fact]
    public void FixAllSafeIssuesAppliesDescendingEditsWithoutOverlapStripsBomAndCrlfAndSkipsReadOnlyAndUnsafe()
    {
        string installDir = Path.Combine(_tempRoot, "game-install");
        string baseDir = Path.Combine(installDir, "modules", "base");
        string packDir = Path.Combine(_tempRoot, "bulk-pack");

        WriteFile(Path.Combine(installDir, "config", "modules.toml"), "LoadOrder = [\"base\"]\n");
        WriteFile(Path.Combine(baseDir, "manifest.toml"), "ID = \"base\"\nName = \"Base\"\n");
        WriteFileWithBom(
            Path.Combine(baseDir, "assets", "bricks", "base_brick.toml"),
            "[[Bricks]]\r\nID = \"wb:base_brick\"\r\nColor = \"ff0000\"\r\n");

        WriteFile(Path.Combine(packDir, "manifest.toml"), "ID = \"bulk.pack\"\nName = \"Bulk Pack\"\n");
        string assets = Path.Combine(packDir, "assets");
        WriteBytes(Path.Combine(assets, "textures", "items", "Drill.png"), [1, 2, 3]);
        WriteBytes(Path.Combine(assets, "textures", "block", "hull.png"), [1, 2, 3]);
        WriteFile(
            Path.Combine(assets, "lang", "en", "item.toml"),
            "\"Language\" = \"en\"\n\n[Translations]\n\"item.drill\" = \"Drill\"\n");

        string itemPath = Path.Combine(assets, "items", "drill.toml");
        WriteFileWithBom(
            itemPath,
            "[[Items]]\r\n" +
            "ID   = \"wb:drill\"\r\n" +
            "Name = \"ITEM.DRILL\"\r\n" +
            "Icon = \"textures\\\\items\\\\drill.png\"\r\n");

        string brickPath = Path.Combine(assets, "bricks", "core.toml");
        WriteFile(
            brickPath,
            "[[Bricks]]\r\n" +
            "ID    = \"wb:core\"\r\n" +
            "Shape = \"Blcok\"\r\n" +
            "Color = \"#f80\"\r\n" +
            "[Bricks.Textures]\r\n" +
            "Default = [\"block/hull.n.png\"]\r\n");

        string tagsPath = Path.Combine(assets, "lang", "tags", "bricks.toml");
        WriteFile(
            tagsPath,
            "\"Language\" = \"\"\n\n" +
            "[Tags]\n" +
            "\"brick.a\" = [\"WB:CORE\", \"WB:CORE\"]\n" +
            "\"brick.b\" = [\"wb:core\", \"wb:core\", \"wb:core\"]\n");

        var loader = new WorkspaceLoader();
        var validator = new WorkspaceValidator();
        WorkspaceIndex ws = loader.LoadPack(packDir, installDir).Value;
        ValidationReport before = validator.Validate(ws);

        Result<BulkFixResult> bulk = ValidationBulkActions.FixAllSafeIssues(ws, before.Findings);
        Assert.True(bulk.Success, bulk.Message);
        Assert.True(bulk.Value.AppliedCount >= 7);
        Assert.Equal(3, bulk.Value.ModifiedFileCount);
        Assert.True(bulk.Value.SkippedReadOnlyCount >= 3);

        byte[] itemBytes = File.ReadAllBytes(itemPath);
        Assert.False(itemBytes.Length >= 3 && itemBytes[0] == 0xEF && itemBytes[1] == 0xBB && itemBytes[2] == 0xBF);
        string itemText = File.ReadAllText(itemPath);
        Assert.DoesNotContain("\r\n", itemText, StringComparison.Ordinal);
        Assert.Contains("Name = \"item.drill\"", itemText, StringComparison.Ordinal);
        Assert.Contains("Icon = \"items/Drill.png\"", itemText, StringComparison.Ordinal);

        string brickText = File.ReadAllText(brickPath);
        Assert.DoesNotContain("\r\n", brickText, StringComparison.Ordinal);
        Assert.Contains("Color = \"#FF8800\"", brickText, StringComparison.Ordinal);
        Assert.Contains("Default = [\"hull\"]", brickText, StringComparison.Ordinal);
        //  Unsafe ENUM001 fix was NOT auto-applied by FixAllSafeIssues.
        Assert.Contains("Shape = \"Blcok\"", brickText, StringComparison.Ordinal);

        string tagsText = File.ReadAllText(tagsPath);
        Assert.Contains("\"brick.a\" = [\"wb:core\"]", tagsText, StringComparison.Ordinal);
        Assert.Contains("\"brick.b\" = [\"wb:core\"]", tagsText, StringComparison.Ordinal);

        //  No safe writable findings remain after bulk fix.
        WorkspaceIndex wsAfter = loader.LoadPack(packDir, installDir).Value;
        ValidationReport after = validator.Validate(wsAfter);
        Assert.DoesNotContain(after.Findings, f => f.RootId == "bulk.pack" && f.IsSafeBulkFix);
        Assert.Contains(after.Findings, f => f.RootId == "bulk.pack" && f.Code == "ENUM001");
    }

    [Fact]
    public void Ref001AndEnum001SuppressAmbiguousTiesAndResolveUniqueBareNamesAcrossBodyAndSkill()
    {
        string pack = CreateMinimalPack("ambiguity-pack");
        string assets = Path.Combine(pack, "assets");

        WriteFile(
            Path.Combine(assets, "bodies", "b1.toml"),
            "[[Bodies]]\nID = \"wb:human\"\nGender = \"Neutral\"\n\n" +
            "[[Bodies]]\nID = \"wb:robot\"\nGender = \"Neutrl\"\n\n" +
            "[[Bodies]]\nID = \"mod:human\"\nGender = \"Male\"\n");
        WriteFile(
            Path.Combine(assets, "skills", "s1.toml"),
            "[[Skills]]\nID = \"mining\"\n[Skills.Levels]\n1 = 100\n\n" +
            "[[Skills]]\nID = \"smithing\"\n[Skills.Levels]\n1 = 100\n");
        WriteFile(
            Path.Combine(assets, "bricks", "b.toml"),
            "[[Bricks]]\nID = \"wb:core\"\nShape = \"block\"\n\n" +
            "[[Bricks]]\nID = \"wb:cork\"\nShape = \"block\"\n\n" +
            "[[Bricks]]\nID = \"wb:unique_steel\"\nShape = \"block\"\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        ValidationReport report = new WorkspaceValidator().Validate(ws);

        Assert.Contains(report.Findings, static f => f.Code == "ENUM001" && f.Field == "Gender" && f.SuggestedFixValue == "Neutral");
        Assert.Equal("wb:unique_steel", WorkspaceValidator.SuggestAssetReference(ws, AssetKind.Brick, "unique_steel"));
        Assert.Null(WorkspaceValidator.SuggestAssetReference(ws, AssetKind.Body, "human"));
        Assert.Equal("wb:robot", WorkspaceValidator.SuggestAssetReference(ws, AssetKind.Body, "wb:robt"));
        Assert.Equal("mining", WorkspaceValidator.SuggestAssetReference(ws, AssetKind.Skill, "minng"));
        Assert.Null(WorkspaceValidator.SuggestAssetReference(ws, AssetKind.Brick, "wb:cor"));
    }

    [Fact]
    public void ApplySingleFixHandlesBomCrlfEmptySkillLevelsTableMultilineTagAndNormalizedEnum()
    {
        string pack = CreateMinimalPack("extra-single-pack");
        string assets = Path.Combine(pack, "assets");
        WriteBytes(Path.Combine(assets, "textures", "block", "hull.png"), [1, 2, 3]);

        string brickPath = Path.Combine(assets, "bricks", "brick.toml");
        WriteFileWithBom(
            brickPath,
            "[[Bricks]]\r\n" +
            "ID    = \"wb:b1\"\r\n" +
            "Shape = \"b_lock\"\r\n" +
            "[Bricks.Sounds]\r\n" +
            "Plce = [\"missing_sound\"]\r\n");

        string skillPath = Path.Combine(assets, "skills", "skill.toml");
        WriteFile(
            skillPath,
            "[[Skills]]\n" +
            "ID = \"alchemy\"\n" +
            "[Skills.Levels]\n");

        string tagsPath = Path.Combine(assets, "lang", "tags", "bricks.toml");
        WriteFile(
            tagsPath,
            "\"Language\" = \"\"\n\n" +
            "[Tags]\n" +
            "\"brick.multi\" = [\n" +
            "    \"wb:b1\",\n" +
            "    \"wb:b1\",\n" +
            "]\n");

        var loader = new WorkspaceLoader();
        var validator = new WorkspaceValidator();
        WorkspaceIndex ws = loader.LoadPack(pack).Value;
        ValidationReport rep = validator.Validate(ws);

        //  Apply StripUtf8Bom and NormalizeLineEndings individually.
        Assert.True(ValidationBulkActions.ApplySingleFix(ws, rep.Findings.First(f => f.Code == "FMT001")).Value);
        byte[] afterBom = File.ReadAllBytes(brickPath);
        Assert.False(afterBom.Length >= 3 && afterBom[0] == 0xEF && afterBom[1] == 0xBB && afterBom[2] == 0xBF);

        Assert.True(ValidationBulkActions.ApplySingleFix(ws, rep.Findings.First(f => f.Code == "FMT002")).Value);
        Assert.DoesNotContain("\r\n", File.ReadAllText(brickPath), StringComparison.Ordinal);

        //  Reload for fresh spans and apply normalized exact enum (IsSafeBulkFix == true) and sound key enum.
        ws = loader.LoadPack(pack).Value;
        rep = validator.Validate(ws);

        ValidationFinding normEnum = rep.Findings.First(f => f.Code == "ENUM001" && f.Field == "Shape");
        Assert.True(normEnum.IsSafeBulkFix);
        Assert.Equal("Block", normEnum.SuggestedFixValue);
        Assert.True(ValidationBulkActions.ApplySingleFix(ws, normEnum).Value);
        Assert.Contains("Shape = \"Block\"", File.ReadAllText(brickPath), StringComparison.Ordinal);

        ws = loader.LoadPack(pack).Value;
        rep = validator.Validate(ws);
        ValidationFinding soundKeyEnum = rep.Findings.First(f => f.Code == "ENUM001" && f.Field == "Sounds.Plce");
        Assert.Equal("Place", soundKeyEnum.SuggestedFixValue);
        Assert.True(ValidationBulkActions.ApplySingleFix(ws, soundKeyEnum).Value);
        Assert.Contains("Place = [\"missing_sound\"]", File.ReadAllText(brickPath), StringComparison.Ordinal);

        //  Empty [Skills.Levels] table gets 1 = 100 inserted inside existing table without duplicating header.
        ValidationFinding emptyLevels = rep.Findings.First(f => f.Code == "SKILL001");
        Assert.True(ValidationBulkActions.ApplySingleFix(ws, emptyLevels).Value);
        string skillText = File.ReadAllText(skillPath);
        Assert.Contains("[Skills.Levels]\n1 = 100", skillText, StringComparison.Ordinal);
        Assert.Equal(1, skillText.Split("[Skills.Levels]", StringSplitOptions.None).Length - 1);

        //  Multiline TAG001 array removes duplicate element cleanly.
        ValidationFinding tagDup = rep.Findings.First(f => f.Code == "TAG001");
        Assert.True(ValidationBulkActions.ApplySingleFix(ws, tagDup).Value);
        Assert.Equal("\"Language\" = \"\"\n\n[Tags]\n\"brick.multi\" = [\n    \"wb:b1\",\n]\n", File.ReadAllText(tagsPath));
    }

    private string CreateMinimalPack(string name)
    {
        string pack = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(Path.Combine(pack, "assets"));
        WriteFile(Path.Combine(pack, "manifest.toml"), $"ID = \"{name}\"\nName = \"{name}\"\n");
        return pack;
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteFileWithBom(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void WriteBytes(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }
}
