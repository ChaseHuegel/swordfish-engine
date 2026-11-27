using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Shoal.DependencyInjection;
using Shoal.IO;
using Shoal.Modularity;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Serialization.Toml.Mappers;
using Xunit;

namespace Swordfish.Tests;

public class ModulesLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"swordfish-modules-{Path.GetRandomFileName()}");

    [Fact]
    public void Load_ContentOnlyModules_MountsAssetsInLoadOrder()
    {
        string baseModule = CreateContentOnlyModule(_root, "base", ("base-only.txt", "base"), ("shared.txt", "base"));
        string overrideModule = CreateContentOnlyModule(_root, "override", ("override-only.txt", "override"), ("shared.txt", "override"));

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
        string moduleRoot = CreateContentOnlyModule(_root, "assembly", ("marker.txt", "assembly"));
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
        string baseModule = CreateContentOnlyModule(_root, "base", ("base-only.txt", "base"));
        string overrideModule = CreateContentOnlyModule(_root, "override", ("override-only.txt", "override"));

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

    [Fact]
    public void Load_ContentOnlyRoot_RejectsManifestWithAssemblies()
    {
        string moduleRoot = CreateContentOnlyModule(_root, "rejected", ("blocked.txt", "blocked"));
        string assemblyPath = typeof(ModulesLoaderTests).Assembly.Location;
        var manifest = new ModuleManifest("rejected", "Rejected")
        {
            Assemblies = [Path.GetFileName(assemblyPath)],
            AssembliesPath = new PathInfo(Path.GetDirectoryName(assemblyPath)!),
        };
        var modules = new List<DiscoveredModule>
        {
            new(new ParsedFile<ModuleManifest>(new PathInfo(Path.Combine(moduleRoot, "manifest.toml")), manifest), ModuleRootPolicy.ContentOnly),
        };
        var options = new ModuleOptions(false, ["rejected"]);
        var catalog = new ModuleCatalog();
        var loader = new ModulesLoader(NullLogger.Instance, options, modules, catalog);
        var vfs = new VirtualFileSystem();
        int callbackCount = 0;

        loader.Load((manifestFile, _) =>
        {
            callbackCount++;
            vfs.Mount(manifestFile.GetRootPath().At("assets"));
        });

        Assert.Equal(0, callbackCount);
        Assert.Empty(catalog.Modules);
        Assert.False(vfs.FileExists(new PathInfo("blocked.txt")));
    }

    [Fact]
    public void Load_ContentOnlyRoot_AllowsEmptyAssembliesArray()
    {
        string moduleRoot = CreateContentOnlyModule(_root, "empty-assemblies", ("ok.txt", "ok"));
        var manifest = new ModuleManifest("empty-assemblies", "Empty Assemblies")
        {
            Assemblies = [],
        };
        var modules = new List<DiscoveredModule>
        {
            new(new ParsedFile<ModuleManifest>(new PathInfo(Path.Combine(moduleRoot, "manifest.toml")), manifest), ModuleRootPolicy.ContentOnly),
        };
        var options = new ModuleOptions(false, ["empty-assemblies"]);
        var loader = new ModulesLoader(NullLogger.Instance, options, modules);
        var vfs = new VirtualFileSystem();
        var loadedIds = new List<string>();

        loader.Load((manifestFile, assemblies) =>
        {
            Assert.Empty(assemblies);
            loadedIds.Add(manifestFile.Value.ID!);
            vfs.Mount(manifestFile.GetRootPath().At("assets"));
        });

        Assert.Equal(["empty-assemblies"], loadedIds);
        Assert.True(vfs.FileExists(new PathInfo("ok.txt")));
    }

    [Fact]
    public void Merge_AppendsUserLoadOrderAfterInstallAndDeduplicatesFirstOccurrence()
    {
        var install = new ModuleOptions(false, ["core", "game", "core"]);
        var user = new ModuleOptions(false, ["game", "pack.alpha", "pack.beta", "pack.alpha", "", "   "]);

        ModuleOptions? merged = ModuleOptions.Merge(install, user);

        Assert.NotNull(merged);
        Assert.NotNull(merged.LoadOrder);
        Assert.Equal(["core", "game", "pack.alpha", "pack.beta"], merged.LoadOrder);
    }

    [Fact]
    public void Merge_UserConfigCannotEscalateAllowScriptCompilation()
    {
        ModuleOptions? installDisabled = ModuleOptions.Merge(
            new ModuleOptions(false, ["core"]),
            new ModuleOptions(true, ["pack.a"]));
        Assert.NotNull(installDisabled);
        Assert.False(installDisabled.AllowScriptCompilation);

        ModuleOptions? installEnabled = ModuleOptions.Merge(
            new ModuleOptions(true, ["core"]),
            new ModuleOptions(false, ["pack.a"]));
        Assert.NotNull(installEnabled);
        Assert.True(installEnabled.AllowScriptCompilation);

        ModuleOptions? userOnly = ModuleOptions.Merge(
            null,
            new ModuleOptions(true, ["pack.a"]));
        Assert.NotNull(userOnly);
        Assert.False(userOnly.AllowScriptCompilation);
        Assert.NotNull(userOnly.LoadOrder);
        Assert.Equal(["pack.a"], userOnly.LoadOrder);
    }

    [Fact]
    public void Merge_WhenUserOptionsNull_ReturnsInstallOptionsUnchanged()
    {
        var install = new ModuleOptions(true, ["core", "game"]);

        ModuleOptions? merged = ModuleOptions.Merge(install, null);

        Assert.Same(install, merged);
    }

    [Fact]
    public void TomlParse_UserModulesTomlWithOnlyLoadOrder_ParsesCleanly()
    {
        ModuleOptions parsed = Toml.To<ModuleOptions>("LoadOrder = [\"pack.alpha\", \"pack.beta\"]\n");

        Assert.False(parsed.AllowScriptCompilation);
        Assert.NotNull(parsed.LoadOrder);
        Assert.Equal(["pack.alpha", "pack.beta"], parsed.LoadOrder);
    }

    [Fact]
    public void ConfigurationProvider_DiscoversUserModulesAndAdditionalRootsAndMergesLoadOrder()
    {
        string installRoot = Path.Combine(_root, "install");
        string installModules = Path.Combine(installRoot, "modules");
        string installConfig = Path.Combine(installRoot, "config", "modules.toml");
        string userRoot = Path.Combine(_root, "user");
        string userModules = Path.Combine(userRoot, "modules");
        string userPacks = Path.Combine(userRoot, "packs");
        string userConfig = Path.Combine(userRoot, "config", "modules.toml");

        string installModDir = CreateContentOnlyModule(installModules, "install.base", ("shared.txt", "install"), ("install.txt", "1"));
        string userModDir = CreateContentOnlyModule(userModules, "user.mod", ("shared.txt", "user-mod"), ("user-mod.txt", "2"));
        string userPackDir = CreateContentOnlyModule(userPacks, "user.pack", ("shared.txt", "user-pack"), ("user-pack.txt", "3"));

        WriteFile(installConfig, "AllowScriptCompilation = false\nLoadOrder = [\"install.base\"]\n");
        WriteFile(userConfig, "LoadOrder = [\"install.base\", \"user.mod\", \"user.pack\", \"user.mod\"]\n");

        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(userRoot), mountInVfs: true);
        var resolution = new ModuleResolutionOptions
        {
            PrimaryRoot = new ModuleSearchRoot(new PathInfo(installModules), ModuleRootPolicy.Full),
            PrimaryConfigPath = new PathInfo(installConfig),
            AdditionalRoots = [new ModuleSearchRoot(new PathInfo(userPacks), ModuleRootPolicy.ContentOnly)],
        };

        var vfs = new VirtualFileSystem();
        var provider = new ConfigurationProvider(NullLogger.Instance, CreateFileParseService(vfs), vfs, appPaths, resolution);
        var catalog = new ModuleCatalog();
        var loader = new ModulesLoader(NullLogger.Instance, provider, catalog);
        var loadedIds = new List<string>();

        loader.Load((manifestFile, assemblies) =>
        {
            Assert.Empty(assemblies);
            loadedIds.Add(manifestFile.Value.ID!);
            vfs.Mount(manifestFile.GetRootPath().At("assets"));
        });

        Assert.Equal(["install.base", "user.mod", "user.pack"], loadedIds);
        Assert.Collection(
            catalog.Modules,
            module => Assert.Equal(installModDir, module.Root.Value),
            module => Assert.Equal(userModDir, module.Root.Value),
            module => Assert.Equal(userPackDir, module.Root.Value));
        Assert.True(vfs.TryGetFile(new PathInfo("install.txt"), out _));
        Assert.True(vfs.TryGetFile(new PathInfo("user-mod.txt"), out _));
        Assert.True(vfs.TryGetFile(new PathInfo("user-pack.txt"), out _));
        Assert.True(vfs.TryGetFile(new PathInfo("shared.txt"), out PathInfo shared));
        Assert.Equal("user-pack", shared.ReadString());
    }

    [Fact]
    public void ConfigurationProvider_ContentOnlyRoot_RejectsManifestWithAssembliesWhileLoadingValidModules()
    {
        string installModules = Path.Combine(_root, "install", "modules");
        string installConfig = Path.Combine(_root, "install", "config", "modules.toml");
        string userRoot = Path.Combine(_root, "user");
        string userPacks = Path.Combine(userRoot, "packs");
        string userConfig = Path.Combine(userRoot, "config", "modules.toml");

        CreateContentOnlyModule(installModules, "install.base", ("base.txt", "base"));
        CreateContentOnlyModule(userPacks, "pack.valid", ("valid.txt", "valid"));
        CreateAssemblyModule(userPacks, "pack.bad", typeof(ModulesLoaderTests).Assembly.Location, ("bad.txt", "bad"));

        WriteFile(installConfig, "AllowScriptCompilation = false\nLoadOrder = [\"install.base\"]\n");
        WriteFile(userConfig, "LoadOrder = [\"pack.bad\", \"pack.valid\"]\n");

        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(userRoot), mountInVfs: true);
        var resolution = new ModuleResolutionOptions
        {
            PrimaryRoot = new ModuleSearchRoot(new PathInfo(installModules), ModuleRootPolicy.Full),
            PrimaryConfigPath = new PathInfo(installConfig),
            AdditionalRoots = [new ModuleSearchRoot(new PathInfo(userPacks), ModuleRootPolicy.ContentOnly)],
        };

        var vfs = new VirtualFileSystem();
        var provider = new ConfigurationProvider(NullLogger.Instance, CreateFileParseService(vfs), vfs, appPaths, resolution);
        var catalog = new ModuleCatalog();
        var loader = new ModulesLoader(NullLogger.Instance, provider, catalog);
        var loadedIds = new List<string>();

        loader.Load((manifestFile, _) =>
        {
            loadedIds.Add(manifestFile.Value.ID!);
            vfs.Mount(manifestFile.GetRootPath().At("assets"));
        });

        Assert.Equal(["install.base", "pack.valid"], loadedIds);
        Assert.Equal(2, catalog.Modules.Count);
        Assert.DoesNotContain(catalog.Modules, module => module.ID == "pack.bad");
        Assert.True(vfs.FileExists(new PathInfo("valid.txt")));
        Assert.False(vfs.FileExists(new PathInfo("bad.txt")));
    }

    [Fact]
    public void ConfigurationProvider_FullPolicyAdditionalRoot_LoadsManifestWithAssemblies()
    {
        string installModules = Path.Combine(_root, "install", "modules");
        string installConfig = Path.Combine(_root, "install", "config", "modules.toml");
        string userRoot = Path.Combine(_root, "user");
        string extraRoot = Path.Combine(_root, "extra");
        string userConfig = Path.Combine(userRoot, "config", "modules.toml");

        CreateContentOnlyModule(installModules, "install.base", ("base.txt", "base"));
        CreateAssemblyModule(extraRoot, "extra.code", typeof(ModulesLoaderTests).Assembly.Location, ("extra.txt", "extra"));

        WriteFile(installConfig, "AllowScriptCompilation = false\nLoadOrder = [\"install.base\"]\n");
        WriteFile(userConfig, "LoadOrder = [\"extra.code\"]\n");

        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(userRoot), mountInVfs: true);
        var resolution = new ModuleResolutionOptions
        {
            PrimaryRoot = new ModuleSearchRoot(new PathInfo(installModules), ModuleRootPolicy.Full),
            PrimaryConfigPath = new PathInfo(installConfig),
            AdditionalRoots = [new ModuleSearchRoot(new PathInfo(extraRoot), ModuleRootPolicy.Full)],
        };

        var vfs = new VirtualFileSystem();
        var provider = new ConfigurationProvider(NullLogger.Instance, CreateFileParseService(vfs), vfs, appPaths, resolution);
        var loader = new ModulesLoader(NullLogger.Instance, provider, new ModuleCatalog());
        var loadedAssemblies = new Dictionary<string, IReadOnlyList<Assembly>>();

        loader.Load((manifestFile, assemblies) =>
        {
            loadedAssemblies[manifestFile.Value.ID!] = assemblies;
            vfs.Mount(manifestFile.GetRootPath().At("assets"));
        });

        Assert.True(loadedAssemblies.TryGetValue("extra.code", out IReadOnlyList<Assembly>? assemblies));
        Assert.Single(assemblies);
        Assert.Equal(typeof(ModulesLoaderTests).Assembly, assemblies[0]);
        Assert.True(vfs.FileExists(new PathInfo("extra.txt")));
    }

    [Fact]
    public void ConfigurationProvider_DuplicateModuleIdAcrossRoots_PrimaryRootWins()
    {
        string installModules = Path.Combine(_root, "install", "modules");
        string installConfig = Path.Combine(_root, "install", "config", "modules.toml");
        string userRoot = Path.Combine(_root, "user");
        string userModules = Path.Combine(userRoot, "modules");

        CreateContentOnlyModule(installModules, "shared.id", ("origin.txt", "install"));
        CreateContentOnlyModule(userModules, "shared.id", ("origin.txt", "user"));
        WriteFile(installConfig, "AllowScriptCompilation = false\nLoadOrder = [\"shared.id\"]\n");

        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(userRoot), mountInVfs: true);
        var resolution = new ModuleResolutionOptions
        {
            PrimaryRoot = new ModuleSearchRoot(new PathInfo(installModules), ModuleRootPolicy.Full),
            PrimaryConfigPath = new PathInfo(installConfig),
        };

        var vfs = new VirtualFileSystem();
        var provider = new ConfigurationProvider(NullLogger.Instance, CreateFileParseService(vfs), vfs, appPaths, resolution);
        var loader = new ModulesLoader(NullLogger.Instance, provider, new ModuleCatalog());

        loader.Load((manifestFile, _) => vfs.Mount(manifestFile.GetRootPath().At("assets")));

        Assert.True(vfs.TryGetFile(new PathInfo("origin.txt"), out PathInfo file));
        Assert.Equal("install", file.ReadString());
    }

    [Fact]
    public void ConfigurationProvider_WhenNoSecondaryRootExists_PreservesInstallBehavior()
    {
        string installModules = Path.Combine(_root, "install", "modules");
        string installConfig = Path.Combine(_root, "install", "config", "modules.toml");
        string missingUserRoot = Path.Combine(_root, "does-not-exist");

        CreateContentOnlyModule(installModules, "install.only", ("only.txt", "install"));
        WriteFile(installConfig, "AllowScriptCompilation = false\nLoadOrder = [\"install.only\"]\n");

        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(missingUserRoot), mountInVfs: true);
        var resolution = new ModuleResolutionOptions
        {
            PrimaryRoot = new ModuleSearchRoot(new PathInfo(installModules), ModuleRootPolicy.Full),
            PrimaryConfigPath = new PathInfo(installConfig),
        };

        var vfs = new VirtualFileSystem();
        var provider = new ConfigurationProvider(NullLogger.Instance, CreateFileParseService(vfs), vfs, appPaths, resolution);
        var loader = new ModulesLoader(NullLogger.Instance, provider, new ModuleCatalog());
        var loadedIds = new List<string>();

        loader.Load((manifestFile, _) =>
        {
            loadedIds.Add(manifestFile.Value.ID!);
            vfs.Mount(manifestFile.GetRootPath().At("assets"));
        });

        Assert.Equal(["install.only"], loadedIds);
        Assert.True(vfs.FileExists(new PathInfo("only.txt")));
    }

    [Fact]
    public void ConfigurationProvider_WhenUserRootMatchesInstallRoot_KeepsFullPolicy()
    {
        string portableRoot = Path.Combine(_root, "portable");
        string modulesDir = Path.Combine(portableRoot, "modules");
        string configFile = Path.Combine(portableRoot, "config", "modules.toml");

        CreateAssemblyModule(modulesDir, "portable.code", typeof(ModulesLoaderTests).Assembly.Location, ("code.txt", "ok"));
        WriteFile(configFile, "AllowScriptCompilation = false\nLoadOrder = [\"portable.code\"]\n");

        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(portableRoot), mountInVfs: false);
        var resolution = new ModuleResolutionOptions
        {
            PrimaryRoot = new ModuleSearchRoot(new PathInfo(modulesDir), ModuleRootPolicy.Full),
            PrimaryConfigPath = new PathInfo(configFile),
        };

        Assert.Single(resolution.GetSearchRoots(in appPaths));
        Assert.Equal(ModuleRootPolicy.Full, resolution.GetSearchRoots(in appPaths)[0].Policy);
        Assert.Null(resolution.GetUserConfigPath(in appPaths));

        var vfs = new VirtualFileSystem();
        var provider = new ConfigurationProvider(NullLogger.Instance, CreateFileParseService(vfs), vfs, appPaths, resolution);
        var loader = new ModulesLoader(NullLogger.Instance, provider, new ModuleCatalog());
        int loadedAssemblyCount = 0;

        loader.Load((_, assemblies) => loadedAssemblyCount += assemblies.Count);

        Assert.Equal(1, loadedAssemblyCount);
    }

    [Fact]
    public void ConfigurationProvider_CorruptUserModulesToml_FallsBackToInstallOptions()
    {
        string installModules = Path.Combine(_root, "install", "modules");
        string installConfig = Path.Combine(_root, "install", "config", "modules.toml");
        string userRoot = Path.Combine(_root, "user");
        string userConfig = Path.Combine(userRoot, "config", "modules.toml");

        CreateContentOnlyModule(installModules, "install.base", ("base.txt", "base"));
        WriteFile(installConfig, "AllowScriptCompilation = false\nLoadOrder = [\"install.base\"]\n");
        WriteFile(userConfig, "LoadOrder = [\"unterminated\n");

        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(userRoot), mountInVfs: true);
        var resolution = new ModuleResolutionOptions
        {
            PrimaryRoot = new ModuleSearchRoot(new PathInfo(installModules), ModuleRootPolicy.Full),
            PrimaryConfigPath = new PathInfo(installConfig),
        };

        var vfs = new VirtualFileSystem();
        var provider = new ConfigurationProvider(NullLogger.Instance, CreateFileParseService(vfs), vfs, appPaths, resolution);

        ModuleOptions? options = provider.GetModuleOptions()?.Value;
        Assert.NotNull(options);
        Assert.NotNull(options.LoadOrder);
        Assert.Equal(["install.base"], options.LoadOrder);
    }

    [Fact]
    public void ConfigurationProvider_EmptyUserModulesToml_KeepsInstallLoadOrder()
    {
        string installModules = Path.Combine(_root, "install", "modules");
        string installConfig = Path.Combine(_root, "install", "config", "modules.toml");
        string userRoot = Path.Combine(_root, "user");
        string userConfig = Path.Combine(userRoot, "config", "modules.toml");

        CreateContentOnlyModule(installModules, "install.base", ("base.txt", "base"));
        WriteFile(installConfig, "AllowScriptCompilation = false\nLoadOrder = [\"install.base\"]\n");
        WriteFile(userConfig, "# empty user modules config\n");

        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(userRoot), mountInVfs: true);
        var resolution = new ModuleResolutionOptions
        {
            PrimaryRoot = new ModuleSearchRoot(new PathInfo(installModules), ModuleRootPolicy.Full),
            PrimaryConfigPath = new PathInfo(installConfig),
        };

        var vfs = new VirtualFileSystem();
        var provider = new ConfigurationProvider(NullLogger.Instance, CreateFileParseService(vfs), vfs, appPaths, resolution);

        ModuleOptions? options = provider.GetModuleOptions()?.Value;
        Assert.NotNull(options);
        Assert.NotNull(options.LoadOrder);
        Assert.Equal(["install.base"], options.LoadOrder);
    }

    [Fact]
    public void ConfigurationProvider_WhenMergeUserLoadOrderDisabled_IgnoresUserModulesToml()
    {
        string installModules = Path.Combine(_root, "install", "modules");
        string installConfig = Path.Combine(_root, "install", "config", "modules.toml");
        string userRoot = Path.Combine(_root, "user");
        string userConfig = Path.Combine(userRoot, "config", "modules.toml");

        CreateContentOnlyModule(installModules, "install.base", ("base.txt", "base"));
        WriteFile(installConfig, "AllowScriptCompilation = false\nLoadOrder = [\"install.base\"]\n");
        WriteFile(userConfig, "LoadOrder = [\"user.pack\"]\n");

        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(userRoot), mountInVfs: true);
        var resolution = new ModuleResolutionOptions
        {
            PrimaryRoot = new ModuleSearchRoot(new PathInfo(installModules), ModuleRootPolicy.Full),
            PrimaryConfigPath = new PathInfo(installConfig),
            MergeUserLoadOrder = false,
        };

        var vfs = new VirtualFileSystem();
        var provider = new ConfigurationProvider(NullLogger.Instance, CreateFileParseService(vfs), vfs, appPaths, resolution);

        ModuleOptions? options = provider.GetModuleOptions()?.Value;
        Assert.NotNull(options);
        Assert.NotNull(options.LoadOrder);
        Assert.Equal(["install.base"], options.LoadOrder);
    }

    [Fact]
    public void ModuleResolutionOptions_AllowsDisablingOverridingAndDeduplicatingRoots()
    {
        string installModules = Path.Combine(_root, "install", "modules");
        string userRoot = Path.Combine(_root, "user");
        string packsDir = Path.Combine(userRoot, "packs");
        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(userRoot), mountInVfs: true);

        var disabled = new ModuleResolutionOptions
        {
            PrimaryRoot = new ModuleSearchRoot(new PathInfo(installModules), ModuleRootPolicy.Full),
            UserDataModulesPolicy = null,
        };
        Assert.Single(disabled.GetSearchRoots(in appPaths));

        var overridden = new ModuleResolutionOptions
        {
            PrimaryRoot = new ModuleSearchRoot(new PathInfo(installModules), ModuleRootPolicy.Full),
            AdditionalRoots =
            [
                new ModuleSearchRoot(appPaths.ModulesRoot, ModuleRootPolicy.Full),
                new ModuleSearchRoot(new PathInfo(packsDir), ModuleRootPolicy.ContentOnly),
                new ModuleSearchRoot(new PathInfo(packsDir + Path.DirectorySeparatorChar), ModuleRootPolicy.Full),
            ],
        };
        IReadOnlyList<ModuleSearchRoot> roots = overridden.GetSearchRoots(in appPaths);
        Assert.Equal(3, roots.Count);
        Assert.Equal(ModuleRootPolicy.Full, roots[1].Policy);
        Assert.Equal(ModuleRootPolicy.ContentOnly, roots[2].Policy);
    }

    private static VirtualFileParseService CreateFileParseService(VirtualFileSystem vfs)
    {
        new PathTomlMapper().Register();
        IFileParser[] parsers =
        [
            new TomlParser<ModuleManifest>(),
            new TomlParser<ModuleOptions>(),
        ];
        return new VirtualFileParseService(parsers, vfs);
    }

    private static string CreateContentOnlyModule(string containerRoot, string id, params (string Name, string Contents)[] assets)
    {
        string moduleRoot = Path.Combine(containerRoot, id);
        string assetsRoot = Path.Combine(moduleRoot, "assets");
        Directory.CreateDirectory(assetsRoot);
        File.WriteAllText(Path.Combine(moduleRoot, "manifest.toml"), $"ID = \"{id}\"\nName = \"{id}\"\n");

        foreach ((string name, string contents) in assets)
        {
            File.WriteAllText(Path.Combine(assetsRoot, name), contents);
        }

        return moduleRoot;
    }

    private static string CreateAssemblyModule(string containerRoot, string id, string assemblyPath, params (string Name, string Contents)[] assets)
    {
        string moduleRoot = Path.Combine(containerRoot, id);
        string assetsRoot = Path.Combine(moduleRoot, "assets");
        Directory.CreateDirectory(assetsRoot);

        string assembliesDir = Path.GetDirectoryName(assemblyPath)!.Replace("\\", "\\\\");
        string assemblyFile = Path.GetFileName(assemblyPath);
        string manifestText =
            $"ID = \"{id}\"\n" +
            $"Name = \"{id}\"\n" +
            $"AssembliesPath = \"{assembliesDir}\"\n" +
            $"Assemblies = [\"{assemblyFile}\"]\n";
        File.WriteAllText(Path.Combine(moduleRoot, "manifest.toml"), manifestText);

        foreach ((string name, string contents) in assets)
        {
            File.WriteAllText(Path.Combine(assetsRoot, name), contents);
        }

        return moduleRoot;
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
