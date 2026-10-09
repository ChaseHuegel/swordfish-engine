using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Permissions;
using Xunit;

namespace Swordfish.Tests;

public sealed class PermissionFileLoaderTests : IDisposable
{
    private readonly string _root;

    public PermissionFileLoaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wb-permissions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void LoadsAssetsAndConfigTogether()
    {
        string assets = Path.Combine(_root, "assets");
        string config = Path.Combine(_root, "config", "permissions");
        Directory.CreateDirectory(Path.Combine(assets, "permissions"));
        Directory.CreateDirectory(config);

        File.WriteAllText(Path.Combine(assets, "permissions", "default.toml"), """
            [Groups.default]
            Permissions = ["baseline"]
            """);
        File.WriteAllText(Path.Combine(config, "admins.toml"), """
            [Users."admin-user"]
            Permissions = ["waywardbeyond.level.save"]
            """);

        var vfs = new VirtualFileSystem();
        Assert.True(vfs.Mount(new PathInfo(assets)).Success);

        var parseService = new VirtualFileParseService([new TomlParser<PermissionFile>()], vfs);
        var loader = new PermissionFileLoader(vfs, parseService, NullLogger<PermissionFileLoader>.Instance, new PermissionLoadOptions
        {
            AssetRoot = new PathInfo("permissions/"),
            ConfigRoot = new PathInfo(config),
        });

        var diagnostics = new PermissionDiagnostics();
        IReadOnlyList<SourcedPermissionFile> files = loader.Load(diagnostics);
        PermissionPolicy policy = PermissionPolicy.Create(files, diagnostics);

        Assert.Equal(2, files.Count);
        Assert.False(diagnostics.HasIssues);
        Assert.True(policy.HasPermission("admin-user", "waywardbeyond.level.save"));
        Assert.True(policy.HasPermission("admin-user", "baseline"));
        Assert.True(policy.HasPermission("nobody", "baseline"));
        Assert.False(policy.HasPermission("nobody", "waywardbeyond.level.save"));
    }

    [Fact]
    public void SkipsUnparseableFiles()
    {
        string config = Path.Combine(_root, "config", "permissions");
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, "broken.toml"), "this is not = = toml");
        File.WriteAllText(Path.Combine(config, "good.toml"), """
            [Groups.default]
            Permissions = ["baseline"]
            """);

        var vfs = new VirtualFileSystem();
        var parseService = new VirtualFileParseService([new TomlParser<PermissionFile>()], vfs);
        var loader = new PermissionFileLoader(vfs, parseService, NullLogger<PermissionFileLoader>.Instance, new PermissionLoadOptions
        {
            ConfigRoot = new PathInfo(config),
        });

        var diagnostics = new PermissionDiagnostics();
        IReadOnlyList<SourcedPermissionFile> files = loader.Load(diagnostics);
        PermissionPolicy policy = PermissionPolicy.Create(files, diagnostics);

        Assert.Single(files);
        Assert.True(diagnostics.Errors.Count > 0);
        Assert.True(policy.HasPermission("nobody", "baseline"));
    }
}
