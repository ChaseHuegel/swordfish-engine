using Swordfish.IO;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Validation;
using WaywardBeyond.AssetTools.Workspace;
using WaywardBeyond.Bodies;
using WaywardBeyond.Bricks;
using WaywardBeyond.Items;
using WaywardBeyond.Skills;

namespace WaywardBeyond.AssetTools.Tests.Workspace;

/// <summary>
///     Verifies multi-root workspace loading, override provenance, inbound references, and game install discovery.
/// </summary>
public sealed class WorkspaceAndLocatorTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary directory for pack and install locator tests.
    /// </summary>
    public WorkspaceAndLocatorTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-ws-tests-{Guid.NewGuid():N}");
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
    public void ShippedRepoLoadsInConfiguredOrderWithReferencesAndZeroFatalFindings()
    {
        string repoRoot = FindRepositoryRoot();
        var loader = new WorkspaceLoader();
        Result<WorkspaceIndex> loaded = loader.LoadRepo(repoRoot);

        Assert.True(loaded.Success, loaded.Message);
        WorkspaceIndex workspace = loaded.Value;

        Assert.Equal(WorkspaceMode.Repo, workspace.Mode);
        Assert.True(workspace.ModulesConfig.HasValue);

        List<string> enabledRootIds = [.. workspace.Roots.Where(static r => r.IsEnabled).Select(static r => r.Id)];
        Assert.Equal(
            [
                "swordfish",
                "waywardbeyond.bricks",
                "waywardbeyond.items",
                "waywardbeyond.skills",
                "waywardbeyond.bodies",
                "waywardbeyond.client",
                "waywardbeyond.server"
            ],
            enabledRootIds);

        Assert.NotEmpty(workspace.GetWinningAssetsByKind(AssetKind.Brick));
        Assert.NotEmpty(workspace.GetWinningAssetsByKind(AssetKind.Item));
        Assert.NotEmpty(workspace.GetWinningAssetsByKind(AssetKind.Paint));
        Assert.NotEmpty(workspace.GetWinningAssetsByKind(AssetKind.Skill));
        Assert.NotEmpty(workspace.GetWinningAssetsByKind(AssetKind.Body));
        Assert.NotEmpty(workspace.GetWinningAssetsByKind(AssetKind.Material));
        Assert.Equal("missing", workspace.OrderedAlbedoLayerNames[0]);
        Assert.True(workspace.OrderedAlbedoLayerNames.Count > 20);

        IReadOnlyList<InboundReference> coreRefs = workspace.GetInboundReferences("wb:core");
        Assert.Contains(coreRefs, static r => r.SourceKind == AssetKind.Item && r.FieldName == "Placeable.ID");
        Assert.Contains(coreRefs, static r => r.SourceKind == AssetKind.Tag);

        var validator = new WorkspaceValidator();
        ValidationReport report = validator.Validate(workspace);
        Assert.Equal(0, report.FatalCount);
        Assert.Equal(0, report.BrokenCount);
    }

    [Fact]
    public void PackModeTracksMultiRootOverridesAndShadowedProviders()
    {
        string installDir = Path.Combine(_tempRoot, "game-install");
        string baseModule = Path.Combine(installDir, "modules", "base");
        string packDir = Path.Combine(_tempRoot, "my-pack");

        Directory.CreateDirectory(Path.Combine(installDir, "config"));
        Directory.CreateDirectory(Path.Combine(baseModule, "assets", "bricks"));
        Directory.CreateDirectory(Path.Combine(packDir, "assets", "bricks"));

        File.WriteAllText(Path.Combine(installDir, "config", "modules.toml"), "LoadOrder = [\"base\"]\n");
        File.WriteAllText(Path.Combine(baseModule, "manifest.toml"), "ID = \"base\"\nName = \"Base Game\"\n");
        File.WriteAllText(
            Path.Combine(baseModule, "assets", "bricks", "core.toml"),
            "[[Bricks]]\nID = \"wb:core\"\nShape = \"block\"\n");

        File.WriteAllText(Path.Combine(packDir, "manifest.toml"), "ID = \"my.pack\"\nName = \"My Pack\"\n");
        File.WriteAllText(
            Path.Combine(packDir, "assets", "bricks", "core.toml"),
            "[[Bricks]]\nID = \"wb:core\"\nShape = \"any\"\n");

        var loader = new WorkspaceLoader();
        Result<WorkspaceIndex> loaded = loader.LoadPack(packDir, installDir);

        Assert.True(loaded.Success, loaded.Message);
        WorkspaceIndex workspace = loaded.Value;

        Assert.Equal(WorkspaceMode.Pack, workspace.Mode);
        Assert.Equal(2, workspace.Roots.Count);
        Assert.False(workspace.Roots[0].IsWritable);
        Assert.True(workspace.Roots[1].IsWritable);

        Result<IndexedAsset> core = workspace.GetAsset(AssetKind.Brick, "wb:core");
        Assert.True(core.Success);
        Assert.Equal("my.pack", core.Value.Provenance.ProviderRootId);
        Assert.True(core.Value.Provenance.IsOverride);
        Assert.Single(core.Value.Provenance.ShadowedProviders);
        Assert.Equal("base", core.Value.Provenance.ShadowedProviders[0].RootId);
    }

    [Fact]
    public void InstallLocatorDiscoversSteamAndProtonAndNeverGuessesOnAmbiguity()
    {
        string steamRoot = Path.Combine(_tempRoot, "steam");
        string extraLib = Path.Combine(_tempRoot, "steam-extra");
        string nativeInstall = Path.Combine(extraLib, "steamapps", "common", "Wayward Beyond");
        string protonInstall = Path.Combine(
            steamRoot,
            "steamapps",
            "compatdata",
            "123456",
            "pfx",
            "drive_c",
            "Program Files (x86)",
            "Steam",
            "steamapps",
            "common",
            "Wayward Beyond");

        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
        Directory.CreateDirectory(Path.Combine(nativeInstall, "modules"));
        Directory.CreateDirectory(Path.Combine(protonInstall, "config"));
        File.WriteAllText(Path.Combine(protonInstall, "config", "modules.toml"), "LoadOrder = []\n");

        string vdf = $"\"libraryfolders\"\n{{\n  \"0\"\n  {{\n    \"path\" \"{extraLib.Replace("\\", "\\\\")}\"\n  }}\n}}\n";
        File.WriteAllText(Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"), vdf);

        var locator = new InstallLocator();
        InstallLocateResult result = locator.Locate(new InstallSearchRoots([steamRoot], [], ProbeWindowsRegistry: false));

        Assert.Equal(2, result.Candidates.Count);
        Assert.True(result.IsAmbiguous);
        Assert.Null(result.SingleMatch);
        Assert.Contains(result.Candidates, c => c.Source == "SteamLibrary" && c.InstallPath == Path.GetFullPath(nativeInstall));
        Assert.Contains(result.Candidates, c => c.Source == "ProtonCompatdata" && c.InstallPath == Path.GetFullPath(protonInstall));

        InstallLocateResult single = locator.Locate(new InstallSearchRoots([], [nativeInstall], ProbeWindowsRegistry: false));
        Assert.False(single.IsAmbiguous);
        Assert.True(single.SingleMatch.HasValue);
        Assert.Equal(Path.GetFullPath(nativeInstall), single.SingleMatch.Value.InstallPath);
    }

    [Fact]
    public void DualTomlParserIsThreadSafeUnderConcurrentDeserialization()
    {
        const string brickToml = "[[Bricks]]\nID = \"wb:core\"\nShape = \"block\"\n";
        const string itemToml = "[[Items]]\nID = \"wb:core\"\nName = \"item.core\"\nMaxStack = 100\n";
        const string paintToml = "[[Paints]]\nID = \"wb:rust\"\nColor = \"#be4a2f\"\n";
        const string skillToml = "[[Skills]]\nID = \"mining\"\n[Skills.Levels]\n1 = 100\n";
        const string bodyToml = "[[Bodies]]\nID = \"wb:human\"\nGender = \"Neutral\"\n";
        const string materialToml = "Shader = \"textured\"\nTextures = [\"block/core.png\"]\n";

        var errors = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 64, i =>
        {
            switch (i % 6)
            {
                case 0:
                {
                    DualTomlParseResult<BrickDefinitions> r = DualTomlParser.Parse<BrickDefinitions>(brickToml);
                    if (!r.AuthoritativeSuccess || !r.StrictSuccess || r.AuthoritativeValue.Bricks.Count != 1)
                    {
                        errors.Add($"Brick parse failed: {r.AuthoritativeError} / {r.StrictError}");
                    }

                    break;
                }
                case 1:
                {
                    DualTomlParseResult<ItemDefinitions> r = DualTomlParser.Parse<ItemDefinitions>(itemToml);
                    if (!r.AuthoritativeSuccess || !r.StrictSuccess || r.AuthoritativeValue.Items.Count != 1)
                    {
                        errors.Add($"Item parse failed: {r.AuthoritativeError} / {r.StrictError}");
                    }

                    break;
                }
                case 2:
                {
                    DualTomlParseResult<PaintDefinitions> r = DualTomlParser.Parse<PaintDefinitions>(paintToml);
                    if (!r.AuthoritativeSuccess || !r.StrictSuccess || r.AuthoritativeValue.Paints?.Count != 1)
                    {
                        errors.Add($"Paint parse failed: {r.AuthoritativeError} / {r.StrictError}");
                    }

                    break;
                }
                case 3:
                {
                    DualTomlParseResult<SkillDefinitions> r = DualTomlParser.Parse<SkillDefinitions>(skillToml);
                    if (!r.AuthoritativeSuccess || !r.StrictSuccess || r.AuthoritativeValue.Skills?.Count != 1)
                    {
                        errors.Add($"Skill parse failed: {r.AuthoritativeError} / {r.StrictError}");
                    }

                    break;
                }
                case 4:
                {
                    DualTomlParseResult<BodyDefinitions> r = DualTomlParser.Parse<BodyDefinitions>(bodyToml);
                    if (!r.AuthoritativeSuccess || !r.StrictSuccess || r.AuthoritativeValue.Bodies?.Count != 1)
                    {
                        errors.Add($"Body parse failed: {r.AuthoritativeError} / {r.StrictError}");
                    }

                    break;
                }
                default:
                {
                    DualTomlParseResult<MaterialDefinition> r = DualTomlParser.Parse<MaterialDefinition>(materialToml);
                    if (!r.AuthoritativeSuccess || !r.StrictSuccess || r.AuthoritativeValue.Shader != "textured")
                    {
                        errors.Add($"Material parse failed: {r.AuthoritativeError} / {r.StrictError}");
                    }

                    break;
                }
            }
        });

        Assert.Empty(errors);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Swordfish.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
