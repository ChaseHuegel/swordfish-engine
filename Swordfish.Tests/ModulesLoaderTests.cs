using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Shoal.IO;
using Shoal.Modularity;
using Swordfish.Library.IO;
using Xunit;

namespace Swordfish.Tests;

public class ModulesLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"swordfish-modules-{Path.GetRandomFileName()}");

    [Fact]
    public void Load_ContentOnlyModules_MountsAssetsInLoadOrder()
    {
        string baseModule = CreateContentOnlyModule("base", ("base-only.txt", "base"), ("shared.txt", "base"));
        string overrideModule = CreateContentOnlyModule("override", ("override-only.txt", "override"), ("shared.txt", "override"));

        var manifests = new List<ParsedFile<ModuleManifest>>
        {
            new(new PathInfo(Path.Combine(baseModule, "manifest.toml")), new ModuleManifest("base", "Base")),
            new(new PathInfo(Path.Combine(overrideModule, "manifest.toml")), new ModuleManifest("override", "Override")),
        };
        var options = new ModuleOptions(false, ["base", "override"]);
        var loader = new ModulesLoader(NullLogger.Instance, options, manifests);
        var vfs = new VirtualFileSystem();
        var moduleAssemblies = new List<IReadOnlyList<Assembly>>();

        loader.Load((manifestFile, assemblies) =>
        {
            vfs.Mount(manifestFile.GetRootPath().At("assets"));
            moduleAssemblies.Add(assemblies);
        });

        Assert.Equal(2, moduleAssemblies.Count);
        Assert.All(moduleAssemblies, assemblies => Assert.Empty(assemblies));
        Assert.True(vfs.TryGetFile(new PathInfo("base-only.txt"), out _));
        Assert.True(vfs.TryGetFile(new PathInfo("override-only.txt"), out _));

        //  The later module in the load order wins the shadowed file.
        Assert.True(vfs.TryGetFile(new PathInfo("shared.txt"), out PathInfo sharedFile));
        Assert.Equal("override", sharedFile.ReadString());
    }

    [Fact]
    public void Load_ModuleWithAssemblies_PassesAssembliesToCallback()
    {
        string moduleRoot = CreateContentOnlyModule("assembly", ("marker.txt", "assembly"));
        string assemblyPath = typeof(ModulesLoaderTests).Assembly.Location;
        var manifest = new ModuleManifest("assembly", "Assembly")
        {
            Assemblies = [Path.GetFileName(assemblyPath)],
            AssembliesPath = new PathInfo(Path.GetDirectoryName(assemblyPath)!),
        };
        var manifests = new List<ParsedFile<ModuleManifest>>
        {
            new(new PathInfo(Path.Combine(moduleRoot, "manifest.toml")), manifest),
        };
        var options = new ModuleOptions(false, ["assembly"]);
        var loader = new ModulesLoader(NullLogger.Instance, options, manifests);
        var moduleAssemblies = new List<IReadOnlyList<Assembly>>();

        loader.Load((_, assemblies) => moduleAssemblies.Add(assemblies));

        Assert.Single(moduleAssemblies);
        Assert.Single(moduleAssemblies[0]);
        Assert.Equal(typeof(ModulesLoaderTests).Assembly, moduleAssemblies[0][0]);
    }

    [Fact]
    public void Load_RecordsLoadedModulesInCatalog()
    {
        string baseModule = CreateContentOnlyModule("base", ("base-only.txt", "base"));
        string overrideModule = CreateContentOnlyModule("override", ("override-only.txt", "override"));

        var manifests = new List<ParsedFile<ModuleManifest>>
        {
            new(new PathInfo(Path.Combine(baseModule, "manifest.toml")), new ModuleManifest("base", "Base")),
            new(new PathInfo(Path.Combine(overrideModule, "manifest.toml")), new ModuleManifest("override", "Override")),
        };
        var options = new ModuleOptions(false, ["base", "override"]);
        var catalog = new ModuleCatalog();
        var loader = new ModulesLoader(NullLogger.Instance, options, manifests, catalog);

        loader.Load((_, _) => { });

        Assert.Collection(
            catalog.Modules,
            module =>
            {
                Assert.Equal("base", module.ID);
                Assert.Equal("Base", module.Name);
                Assert.Equal(baseModule, module.Root.Value);
            },
            module =>
            {
                Assert.Equal("override", module.ID);
                Assert.Equal("Override", module.Name);
                Assert.Equal(overrideModule, module.Root.Value);
            }
        );
    }

    private string CreateContentOnlyModule(string id, params (string Name, string Contents)[] assets)
    {
        string moduleRoot = Path.Combine(_root, id);
        string assetsRoot = Path.Combine(moduleRoot, "assets");
        Directory.CreateDirectory(assetsRoot);
        File.WriteAllText(Path.Combine(moduleRoot, "manifest.toml"), $"ID = \"{id}\"\nName = \"{id}\"\n");

        foreach ((string name, string contents) in assets)
        {
            File.WriteAllText(Path.Combine(assetsRoot, name), contents);
        }

        return moduleRoot;
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
