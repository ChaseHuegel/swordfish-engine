using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Serialization.Toml.Mappers;
using WaywardBeyond.Config;
using Xunit;

namespace WaywardBeyond.Tests;

public class PackModuleResolutionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"wb-packs-{Path.GetRandomFileName()}");

    [Fact]
    public void CreateOptions_RegistersContentOnlyUserPacksRoot()
    {
        string userRoot = Path.Combine(_root, "user");
        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(userRoot), mountInVfs: true);

        ModuleResolutionOptions options = PackModuleResolution.CreateOptions(appPaths);

        ModuleSearchRoot packsRoot = Assert.Single(options.AdditionalRoots);
        Assert.Equal(Path.Combine(userRoot, "packs"), packsRoot.Path.Value);
        Assert.Equal(ModuleRootPolicy.ContentOnly, packsRoot.Policy);
    }

    [Fact]
    public void CreateOptions_LoadsContentPacksAndRejectsAssembliesInUserPacksRoot()
    {
        string installModules = Path.Combine(_root, "install", "modules");
        string installConfig = Path.Combine(_root, "install", "config", "modules.toml");
        string userRoot = Path.Combine(_root, "user");
        string userPacks = Path.Combine(userRoot, "packs");
        string userConfig = Path.Combine(userRoot, "config", "modules.toml");

        CreatePack(installModules, "waywardbeyond.bricks", ("bricks/stone.toml", "install"));
        CreatePack(userPacks, "my.pack", ("bricks/stone.toml", "pack"), ("bricks/custom.toml", "custom"));
        CreateAssemblyPack(userPacks, "bad.pack", typeof(PackModuleResolutionTests).Assembly.Location, ("bricks/bad.toml", "bad"));

        WriteFile(installConfig, "AllowScriptCompilation = false\nLoadOrder = [\"waywardbeyond.bricks\"]\n");
        WriteFile(userConfig, "LoadOrder = [\"bad.pack\", \"my.pack\"]\n");

        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(userRoot), mountInVfs: true);
        ModuleResolutionOptions gameOptions = PackModuleResolution.CreateOptions(appPaths);
        var resolution = new ModuleResolutionOptions
        {
            PrimaryRoot = new ModuleSearchRoot(new PathInfo(installModules), ModuleRootPolicy.Full),
            PrimaryConfigPath = new PathInfo(installConfig),
            UserDataModulesPolicy = gameOptions.UserDataModulesPolicy,
            MergeUserLoadOrder = gameOptions.MergeUserLoadOrder,
            AdditionalRoots = gameOptions.AdditionalRoots,
        };

        var vfs = new VirtualFileSystem();
        new PathTomlMapper().Register();
        var parseService = new VirtualFileParseService(
            [new TomlParser<ModuleManifest>(), new TomlParser<ModuleOptions>()],
            vfs);

        var provider = new ConfigurationProvider(NullLogger.Instance, parseService, vfs, appPaths, resolution);
        var catalog = new ModuleCatalog();
        var loader = new ModulesLoader(NullLogger.Instance, provider, catalog);
        var loadedIds = new List<string>();

        loader.Load((manifestFile, assemblies) =>
        {
            Assert.Empty(assemblies);
            loadedIds.Add(manifestFile.Value.ID!);
            vfs.Mount(manifestFile.GetRootPath().At("assets"));
        });

        Assert.Equal(["waywardbeyond.bricks", "my.pack"], loadedIds);
        Assert.Collection(
            catalog.Modules,
            module => Assert.Equal("waywardbeyond.bricks", module.ID),
            module =>
            {
                Assert.Equal("my.pack", module.ID);
                Assert.Equal(Path.Combine(userPacks, "my.pack"), module.Root.Value);
            });
        Assert.True(vfs.TryGetFile(new PathInfo("bricks/stone.toml"), out PathInfo stoneFile));
        Assert.Equal("pack", stoneFile.ReadString());
        Assert.True(vfs.FileExists(new PathInfo("bricks/custom.toml")));
        Assert.False(vfs.FileExists(new PathInfo("bricks/bad.toml")));
    }

    private static void CreatePack(string containerRoot, string id, params (string RelativePath, string Contents)[] assets)
    {
        string packRoot = Path.Combine(containerRoot, id);
        Directory.CreateDirectory(packRoot);
        File.WriteAllText(Path.Combine(packRoot, "manifest.toml"), $"ID = \"{id}\"\nName = \"{id}\"\n");

        foreach ((string relativePath, string contents) in assets)
        {
            WriteFile(Path.Combine(packRoot, "assets", relativePath), contents);
        }
    }

    private static void CreateAssemblyPack(string containerRoot, string id, string assemblyPath, params (string RelativePath, string Contents)[] assets)
    {
        string packRoot = Path.Combine(containerRoot, id);
        Directory.CreateDirectory(packRoot);

        string assembliesDir = Path.GetDirectoryName(assemblyPath)!.Replace("\\", "\\\\");
        string assemblyFile = Path.GetFileName(assemblyPath);
        File.WriteAllText(
            Path.Combine(packRoot, "manifest.toml"),
            $"ID = \"{id}\"\nName = \"{id}\"\nAssembliesPath = \"{assembliesDir}\"\nAssemblies = [\"{assemblyFile}\"]\n");

        foreach ((string relativePath, string contents) in assets)
        {
            WriteFile(Path.Combine(packRoot, "assets", relativePath), contents);
        }
    }

    private static void WriteFile(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            //  Best-effort cleanup.
        }
    }
}
