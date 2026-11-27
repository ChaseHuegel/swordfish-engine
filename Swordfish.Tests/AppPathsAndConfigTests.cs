using System;
using System.IO;
using DryIoc;
using Shoal.Extensions.Swordfish;
using Swordfish;
using Swordfish.Library.Configuration;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Serialization.Toml.Mappers;
using Swordfish.Library.Types;
using Swordfish.Settings;
using Xunit;

namespace Swordfish.Tests;

public class AppPathsAndConfigTests
{
    public sealed class TestConfig : Config<TestConfig>
    {
        public DataBinding<string> Value { get; private set; } = new("default");
    }

    [Fact]
    public void ForLocalUser_ResolvesUnderLocalAppDataAndMountsInVfs()
    {
        AppPaths paths = AppPaths.ForLocalUser("SwordfishTestApp");

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        string expectedRoot = string.IsNullOrWhiteSpace(localAppData)
            ? AppContext.BaseDirectory
            : Path.Combine(localAppData, "SwordfishTestApp");

        Assert.Equal(expectedRoot, paths.DataRoot.Value);
        Assert.Equal(Path.Combine(expectedRoot, "config"), paths.ConfigRoot.Value);
        Assert.Equal(Path.Combine(expectedRoot, "logs"), paths.LogsRoot.Value);
        Assert.Equal(Path.Combine(expectedRoot, "modules"), paths.ModulesRoot.Value);

        //  Only a real user directory is mounted as an override, never the install fallback.
        Assert.Equal(!string.IsNullOrWhiteSpace(localAppData), paths.MountInVfs);
    }

    [Fact]
    public void ForLocalUser_CompanyNameNestsAboveApplication()
    {
        AppPaths paths = AppPaths.ForLocalUser("SwordfishTestApp", "SwordfishTestCompany");

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        string expectedRoot = string.IsNullOrWhiteSpace(localAppData)
            ? AppContext.BaseDirectory
            : Path.Combine(localAppData, "SwordfishTestCompany", "SwordfishTestApp");

        Assert.Equal(expectedRoot, paths.DataRoot.Value);
        Assert.Equal(Path.Combine(expectedRoot, "config"), paths.ConfigRoot.Value);
        Assert.Equal(!string.IsNullOrWhiteSpace(localAppData), paths.MountInVfs);
    }

    [Fact]
    public void ForWorkingDirectory_UsesCurrentDirectoryWithoutVfsMount()
    {
        AppPaths paths = AppPaths.ForWorkingDirectory();

        Assert.Equal(Environment.CurrentDirectory, paths.DataRoot.Value);
        Assert.Equal(Path.Combine(Environment.CurrentDirectory, "config"), paths.ConfigRoot.Value);
        Assert.Equal(Path.Combine(Environment.CurrentDirectory, "logs"), paths.LogsRoot.Value);
        Assert.Equal(Path.Combine(Environment.CurrentDirectory, "modules"), paths.ModulesRoot.Value);
        Assert.False(paths.MountInVfs);
    }

    [Fact]
    public void ResolveDataPath_ResolvesRelativeAndPreservesRooted()
    {
        string root = Path.Combine(Path.GetTempPath(), "swordfish-resolve-test");
        AppPaths paths = AppPaths.ForDirectory(new PathInfo(root));

        PathInfo relative = paths.ResolveDataPath(new PathInfo("saves/"));
        Assert.Equal(Path.Combine(root, "saves/"), relative.Value);

        string absolute = Path.Combine(Path.GetTempPath(), "explicit-saves");
        PathInfo rooted = paths.ResolveDataPath(new PathInfo(absolute));
        Assert.Equal(absolute, rooted.Value);
    }

    [Fact]
    public void ConfigSave_CreatesMissingParentDirectories()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var config = new TestConfig
            {
                Path = new PathInfo(Path.Combine(tempRoot, "nested", "config", "test.toml")),
            };
            config.Value.Set("saved");

            config.Save();

            Assert.True(File.Exists(config.Path.Value));
            Assert.Contains("saved", File.ReadAllText(config.Path.Value));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void RegisterConfig_DefaultUserData_ReadsFromVfsAndSavesToUserConfig()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string moduleAssets = Path.Combine(tempRoot, "module_assets");
        string userRoot = Path.Combine(tempRoot, "user_data");
        Directory.CreateDirectory(Path.Combine(moduleAssets, "config"));
        File.WriteAllText(Path.Combine(moduleAssets, "config", "sample.toml"), "Value = \"from-module\"\n");

        try
        {
            using var container = CreateConfigContainer(AppPaths.ForDirectory(new PathInfo(userRoot), mountInVfs: true), moduleAssets, mountUserRoot: false);
            container.RegisterConfig<TestConfig>(file: "sample.toml");

            TestConfig loaded = container.Resolve<TestConfig>();
            Assert.Equal("from-module", loaded.Value.Get());
            Assert.Equal(Path.Combine(userRoot, "config", "sample.toml"), loaded.Path.Value);

            loaded.Value.Set("from-user");
            loaded.Save();

            //  Module asset file must remain untouched; user config file receives the write.
            Assert.Contains("from-module", File.ReadAllText(Path.Combine(moduleAssets, "config", "sample.toml")));
            Assert.Contains("from-user", File.ReadAllText(Path.Combine(userRoot, "config", "sample.toml")));

            //  Mounting the user directory last overrides the module file on subsequent load.
            using var reloadContainer = CreateConfigContainer(AppPaths.ForDirectory(new PathInfo(userRoot), mountInVfs: true), moduleAssets, mountUserRoot: true);
            reloadContainer.RegisterConfig<TestConfig>(file: "sample.toml");

            TestConfig reloaded = reloadContainer.Resolve<TestConfig>();
            Assert.Equal("from-user", reloaded.Value.Get());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void RegisterConfig_InstallSaveLocation_TargetsInstallConfigPath()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string moduleAssets = Path.Combine(tempRoot, "module_assets");
        string userRoot = Path.Combine(tempRoot, "user_data");
        Directory.CreateDirectory(moduleAssets);

        try
        {
            using var container = CreateConfigContainer(AppPaths.ForDirectory(new PathInfo(userRoot), mountInVfs: true), moduleAssets, mountUserRoot: false);
            container.RegisterConfig<TestConfig>(file: "install_only.toml", saveLocation: SaveLocation.Install);

            TestConfig loaded = container.Resolve<TestConfig>();
            Assert.Equal(new PathInfo("config/").At("install_only.toml").Value, loaded.Path.Value);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void RegisterConfig_ExactConfigPathBeatsNestedFileWithSameName()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string moduleAssets = Path.Combine(tempRoot, "module_assets");
        string userRoot = Path.Combine(tempRoot, "user_data");
        Directory.CreateDirectory(Path.Combine(moduleAssets, "config", "nested"));
        Directory.CreateDirectory(Path.Combine(userRoot, "config"));
        File.WriteAllText(Path.Combine(moduleAssets, "config", "nested", "sample.toml"), "Value = \"nested\"\n");
        File.WriteAllText(Path.Combine(userRoot, "config", "sample.toml"), "Value = \"exact\"\n");

        try
        {
            using var container = CreateConfigContainer(AppPaths.ForDirectory(new PathInfo(userRoot), mountInVfs: true), moduleAssets, mountUserRoot: true);
            container.RegisterConfig<TestConfig>(file: "sample.toml");

            TestConfig loaded = container.Resolve<TestConfig>();
            Assert.Equal("exact", loaded.Value.Get());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void RegisterConfig_ActivatesMappersRegisteredAfterStartup()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string moduleAssets = Path.Combine(tempRoot, "module_assets");
        string userRoot = Path.Combine(tempRoot, "user_data");
        Directory.CreateDirectory(Path.Combine(moduleAssets, "config"));
        File.WriteAllText(
            Path.Combine(moduleAssets, "config", "render.toml"),
            "VSync = false\nAntiAliasing = \"None\"\n"
        );

        try
        {
            using var container = new Container();
            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(moduleAssets));
            container.RegisterInstance(vfs);
            container.RegisterInstance(AppPaths.ForDirectory(new PathInfo(userRoot)));
            container.Register<IFileParseService, VirtualFileParseService>(Reuse.Transient);

            container.RegisterTomlMapper<PathTomlMapper>();
            container.RegisterDataBinding<bool>();
            container.RegisterDataBinding<int>();
            container.RegisterDataBinding<float>();

            foreach (ITomlMapper mapper in container.ResolveMany<ITomlMapper>())
            {
                mapper.Register();
            }

            container.RegisterConfig<RenderSettings>(file: "render.toml");

            // EngineContainer.Register adds this mapper after the startup activation pass.
            container.RegisterDataBinding<AntiAliasing>();

            // Module injectors resolve configs from the child modules container.
            using var modulesContainer = container.With();
            RenderSettings loaded = modulesContainer.Resolve<RenderSettings>();

            Assert.False(loaded.VSync.Get());
            Assert.Equal(AntiAliasing.None, loaded.AntiAliasing.Get());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolveAppPaths_NoArguments_UsesLocalUserPaths()
    {
        AppPaths resolved = SwordfishEngine.ResolveAppPaths([]);
        AppPaths expected = AppPaths.ForLocalUser();

        Assert.Equal(expected.DataRoot.Value, resolved.DataRoot.Value);
        Assert.Equal(expected.MountInVfs, resolved.MountInVfs);
    }

    [Fact]
    public void ResolveAppPaths_NoArguments_AppliesCompanyName()
    {
        AppPaths resolved = SwordfishEngine.ResolveAppPaths([], "SwordfishTestCompany");
        AppPaths expected = AppPaths.ForLocalUser(companyName: "SwordfishTestCompany");

        Assert.Equal(expected.DataRoot.Value, resolved.DataRoot.Value);
    }

    [Fact]
    public void ResolveAppPaths_Portable_UsesWorkingDirectory()
    {
        AppPaths resolved = SwordfishEngine.ResolveAppPaths(["--portable"]);

        Assert.Equal(Environment.CurrentDirectory, resolved.DataRoot.Value);
        Assert.False(resolved.MountInVfs);
    }

    [Fact]
    public void ResolveAppPaths_UserData_UsesProvidedDirectory()
    {
        string userData = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        AppPaths resolved = SwordfishEngine.ResolveAppPaths(["--user-data", userData]);

        Assert.Equal(Path.GetFullPath(userData), resolved.DataRoot.Value);
        Assert.True(resolved.MountInVfs);
    }

    [Fact]
    public void ResolveAppPaths_UserDataTakesPrecedenceOverPortable()
    {
        string userData = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        AppPaths resolved = SwordfishEngine.ResolveAppPaths(["--portable", $"--user-data={userData}"]);

        Assert.Equal(Path.GetFullPath(userData), resolved.DataRoot.Value);
    }

    private static Container CreateConfigContainer(AppPaths appPaths, string moduleAssets, bool mountUserRoot)
    {
        var container = new Container();
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(moduleAssets));
        if (mountUserRoot && appPaths.MountInVfs && appPaths.DataRoot.DirectoryExists())
        {
            vfs.Mount(appPaths.DataRoot);
        }

        container.RegisterInstance(appPaths);
        container.RegisterInstance(vfs);
        container.Register<IFileParseService, VirtualFileParseService>(Reuse.Transient);
        container.RegisterDataBinding<string>();
        foreach (ITomlMapper mapper in container.ResolveMany<ITomlMapper>())
        {
            mapper.Register();
        }

        return container;
    }
}
