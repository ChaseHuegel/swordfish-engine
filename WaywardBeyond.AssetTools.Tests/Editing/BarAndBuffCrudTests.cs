using System.Text;
using WaywardBeyond.AssetTools.Cli;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Editing;
using WaywardBeyond.AssetTools.Storage;
using WaywardBeyond.AssetTools.Validation;
using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Tests.Editing;

/// <summary>
///     Verifies bar and buff indexing, validation, scaffolding, cloning, deletion, and CLI listing.
/// </summary>
public sealed class BarAndBuffCrudTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary workspace directory for bar and buff tool tests.
    /// </summary>
    public BarAndBuffCrudTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-bar-buff-tests-{Guid.NewGuid():N}");
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
    public void IndexesBarsAndBuffsExtractsReferencesAndValidatesCleanly()
    {
        string pack = CreateBasePack("bar-buff-index-pack");
        string assets = Path.Combine(pack, "assets");

        WriteFile(
            Path.Combine(assets, "bars", "vitals.toml"),
            "[[Bars]]\n" +
            "ID = \"test:health\"\n" +
            "StatId = \"wb:health\"\n" +
            "MaxStatId = \"wb:max_health\"\n" +
            "DefaultMax = 100.0\n" +
            "IconCount = 10\n" +
            "IconFull = \"ui/hud/heart_full.png\"\n" +
            "IconHalf = \"ui/hud/heart_half.png\"\n" +
            "IconEmpty = \"ui/hud/heart_empty.png\"\n" +
            "WarnThreshold = 0.3\n" +
            "CriticalThreshold = 0.15\n" +
            "Order = 0\n" +
            "HideInCreative = true\n" +
            "ColorTint = \"#FF4444\"\n");

        WriteFile(
            Path.Combine(assets, "buffs", "status.toml"),
            "[[Buffs]]\n" +
            "ID = \"test:well_fed\"\n" +
            "Name = \"buff.well_fed.name\"\n" +
            "Description = \"buff.well_fed.desc\"\n" +
            "Icon = \"icons/buffs/well_fed.png\"\n" +
            "DurationSeconds = 120.0\n" +
            "StackPolicy = \"Refresh\"\n" +
            "\n" +
            "[[Buffs.StatModifiers]]\n" +
            "StatId = \"wb:move_speed\"\n" +
            "PercentDelta = 0.15\n" +
            "\n" +
            "[[Buffs.RegenRates]]\n" +
            "StatId = \"wb:health\"\n" +
            "PerSecond = 2.5\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        ValidationReport report = new WorkspaceValidator().Validate(ws);

        Assert.False(report.BlocksExport);
        Assert.Equal(0, report.FatalCount);
        Assert.Equal(0, report.BrokenCount);
        Assert.True(ws.GetAsset(AssetKind.Bar, "test:health").Success);
        Assert.True(ws.GetAsset(AssetKind.Buff, "test:well_fed").Success);

        Assert.Contains(ws.GetInboundReferences("ui/hud/heart_full.png"), static r => r.SourceKind == AssetKind.Bar && r.SourceAssetId == "test:health" && r.FieldName == "IconFull");
        Assert.Contains(ws.GetInboundReferences("buff.well_fed.name"), static r => r.SourceKind == AssetKind.Buff && r.SourceAssetId == "test:well_fed" && r.FieldName == "Name");
        Assert.Contains(ws.GetInboundReferences("icons/buffs/well_fed.png"), static r => r.SourceKind == AssetKind.Buff && r.SourceAssetId == "test:well_fed" && r.FieldName == "Icon");

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int exit = new CliRunner(stdout, stderr, useColor: false).Run(["ids", pack]);
        Assert.Equal(CliExitCode.CLEAN, exit);
        string output = stdout.ToString();
        Assert.Contains("[Bar]", output, StringComparison.Ordinal);
        Assert.Contains("test:health", output, StringComparison.Ordinal);
        Assert.Contains("[Buff]", output, StringComparison.Ordinal);
        Assert.Contains("test:well_fed", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatesInvalidBarAndBuffDefinitions()
    {
        string pack = CreateBasePack("bar-buff-invalid-pack");
        string assets = Path.Combine(pack, "assets");

        WriteFile(
            Path.Combine(assets, "bars", "broken.toml"),
            "[[Bars]]\n" +
            "ID = \"test:bad_bar\"\n" +
            "StatId = \"\"\n" +
            "DefaultMax = -5.0\n" +
            "IconCount = 0\n" +
            "WarnThreshold = 0.2\n" +
            "CriticalThreshold = 0.5\n" +
            "ColorTint = \"not-a-hex\"\n");

        WriteFile(
            Path.Combine(assets, "buffs", "broken.toml"),
            "[[Buffs]]\n" +
            "ID = \"test:bad_buff\"\n" +
            "Name = \"\"\n" +
            "DurationSeconds = 0.0\n" +
            "StackPolicy = \"StackForever\"\n" +
            "\n" +
            "[[Buffs.StatModifiers]]\n" +
            "StatId = \"wb:strength\"\n" +
            "\n" +
            "[[Buffs.RegenRates]]\n" +
            "StatId = \"wb:health\"\n" +
            "PerSecond = 0.0\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        ValidationReport report = new WorkspaceValidator().Validate(ws);

        Assert.True(report.BlocksExport);
        Assert.Contains(report.Findings, static f => f.Code == "BAR001" && f.Field == "StatId");
        Assert.Contains(report.Findings, static f => f.Code == "BAR001" && f.Field == "DefaultMax");
        Assert.Contains(report.Findings, static f => f.Code == "BAR001" && f.Field == "IconCount");
        Assert.Contains(report.Findings, static f => f.Code == "BAR001" && f.Field == "CriticalThreshold");
        Assert.Contains(report.Findings, static f => f.Code == "BAR001" && f.Field == "ColorTint");

        Assert.Contains(report.Findings, static f => f.Code == "BUFF001" && f.Field == "Name");
        Assert.Contains(report.Findings, static f => f.Code == "BUFF001" && f.Field == "DurationSeconds");
        Assert.Contains(report.Findings, static f => f.Code == "BUFF001" && f.Field == "StackPolicy");
        Assert.Contains(report.Findings, static f => f.Code == "BUFF001" && f.Field == "StatModifiers[0]");
        Assert.Contains(report.Findings, static f => f.Code == "BUFF001" && f.Field == "RegenRates[0].PerSecond");
    }

    [Fact]
    public void ScaffoldsAppendsClonesAndDeletesBarsAndBuffs()
    {
        string pack = CreateBasePack("bar-buff-crud-pack");
        string assets = Path.Combine(pack, "assets");
        string barFile = Path.Combine(assets, "bars", "custom_bars.toml");
        string buffFile = Path.Combine(assets, "buffs", "custom_buffs.toml");

        var scaffolder = new AssetScaffolder();
        var saver = new AtomicFileSaver();
        var cloner = new AssetCloner(saver);
        var deleter = new AssetDeleter(saver);

        //  Bars: scaffold new file, append second entry, clone, delete original.
        TomlDocument createdBar = scaffolder.CreateInNewFile(AssetKind.Bar, "test:stamina", barFile).Value;
        Directory.CreateDirectory(Path.GetDirectoryName(barFile)!);
        File.WriteAllBytes(barFile, createdBar.ToBytes());

        TomlDocument loadedBar = TomlDocument.Load(barFile).Value;
        Assert.True(scaffolder.AppendToExistingFile(AssetKind.Bar, "test:hunger", loadedBar).Success);
        File.WriteAllBytes(barFile, loadedBar.ToBytes());

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        Assert.True(ws.GetAsset(AssetKind.Bar, "test:stamina").Success);
        Assert.True(ws.GetAsset(AssetKind.Bar, "test:hunger").Success);

        IndexedAsset staminaBar = ws.GetAsset(AssetKind.Bar, "test:stamina").Value;
        var clonedBar = cloner.DuplicateAsset(ws, AssetKind.Bar, "test:stamina", barFile, staminaBar.Provenance.TableIndex);
        Assert.True(clonedBar.Success, clonedBar.Message);
        Assert.Equal("test:stamina_2", clonedBar.Value.NewAssetId);

        ws = new WorkspaceLoader().LoadPack(pack).Value;
        Assert.True(ws.GetAsset(AssetKind.Bar, "test:stamina_2").Success);

        var deletedBar = deleter.Delete(ws, AssetKind.Bar, barFile, "test:stamina");
        Assert.True(deletedBar.Success, deletedBar.Message);

        ws = new WorkspaceLoader().LoadPack(pack).Value;
        Assert.False(ws.GetAsset(AssetKind.Bar, "test:stamina").Success);
        Assert.True(ws.GetAsset(AssetKind.Bar, "test:hunger").Success);
        Assert.True(ws.GetAsset(AssetKind.Bar, "test:stamina_2").Success);

        //  Buffs: scaffold new file, append second entry, clone, delete original.
        TomlDocument createdBuff = scaffolder.CreateInNewFile(AssetKind.Buff, "test:energized", buffFile).Value;
        Directory.CreateDirectory(Path.GetDirectoryName(buffFile)!);
        File.WriteAllBytes(buffFile, createdBuff.ToBytes());

        TomlDocument loadedBuff = TomlDocument.Load(buffFile).Value;
        Assert.True(scaffolder.AppendToExistingFile(AssetKind.Buff, "test:chilled", loadedBuff).Success);
        File.WriteAllBytes(buffFile, loadedBuff.ToBytes());

        ws = new WorkspaceLoader().LoadPack(pack).Value;
        Assert.True(ws.GetAsset(AssetKind.Buff, "test:energized").Success);
        Assert.True(ws.GetAsset(AssetKind.Buff, "test:chilled").Success);

        IndexedAsset energizedBuff = ws.GetAsset(AssetKind.Buff, "test:energized").Value;
        var clonedBuff = cloner.DuplicateAsset(ws, AssetKind.Buff, "test:energized", buffFile, energizedBuff.Provenance.TableIndex);
        Assert.True(clonedBuff.Success, clonedBuff.Message);
        Assert.Equal("test:energized_2", clonedBuff.Value.NewAssetId);

        ws = new WorkspaceLoader().LoadPack(pack).Value;
        Assert.True(ws.GetAsset(AssetKind.Buff, "test:energized_2").Success);

        var deletedBuff = deleter.Delete(ws, AssetKind.Buff, buffFile, "test:energized");
        Assert.True(deletedBuff.Success, deletedBuff.Message);

        ws = new WorkspaceLoader().LoadPack(pack).Value;
        Assert.False(ws.GetAsset(AssetKind.Buff, "test:energized").Success);
        Assert.True(ws.GetAsset(AssetKind.Buff, "test:chilled").Success);
        Assert.True(ws.GetAsset(AssetKind.Buff, "test:energized_2").Success);
    }

    private string CreateBasePack(string name)
    {
        string packDir = Path.Combine(_tempRoot, name);
        string assets = Path.Combine(packDir, "assets");
        Directory.CreateDirectory(Path.Combine(assets, "textures", "ui", "hud"));
        Directory.CreateDirectory(Path.Combine(assets, "textures", "icons", "buffs"));
        Directory.CreateDirectory(Path.Combine(assets, "lang", "en"));

        WriteFile(
            Path.Combine(packDir, "pack.toml"),
            "ID = \"" + name + "\"\n" +
            "Namespace = \"test\"\n" +
            "DisplayName = \"" + name + "\"\n" +
            "Author = \"Tester\"\n" +
            "ContentVersion = 1\n");

        WriteFile(
            Path.Combine(assets, "lang", "en", "buffs.toml"),
            "\"Language\" = \"en\"\n\n[Translations]\n" +
            "\"buff.well_fed.name\" = \"Well Fed\"\n" +
            "\"buff.well_fed.desc\" = \"Restores health over time.\"\n");

        byte[] minimalPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        File.WriteAllBytes(Path.Combine(assets, "textures", "ui", "hud", "heart_full.png"), minimalPng);
        File.WriteAllBytes(Path.Combine(assets, "textures", "ui", "hud", "heart_half.png"), minimalPng);
        File.WriteAllBytes(Path.Combine(assets, "textures", "ui", "hud", "heart_empty.png"), minimalPng);
        File.WriteAllBytes(Path.Combine(assets, "textures", "icons", "buffs", "well_fed.png"), minimalPng);

        return packDir;
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content));
    }
}
