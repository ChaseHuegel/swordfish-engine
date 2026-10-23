using System;
using System.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Permissions;
using Xunit;

namespace Swordfish.Tests;

public class PermissionFileLoaderTests
{
    [Fact]
    public void EmptyAssetRootWarns()
    {
        var loader = new PermissionFileLoader(new VirtualFileSystem(), new PermissionTomlParser());
        var diagnostics = new PermissionDiagnostics();

        var files = loader.Load(diagnostics);

        Assert.Empty(files);
        Assert.Contains(diagnostics.Warnings, warning => warning.Contains("permissions/"));
    }

    [Fact]
    public void LoadsEveryTomlUnderTheAssetRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(root, "permissions"));
        try
        {
            File.WriteAllText(Path.Combine(root, "permissions", "b.toml"), "[Groups.default]\nPermissions = [\"b\"]");
            File.WriteAllText(Path.Combine(root, "permissions", "a.toml"), "[Groups.default]\nPermissions = [\"a\"]");
            File.WriteAllText(Path.Combine(root, "permissions", "ignored.txt"), "not a permission file");

            var vfs = new VirtualFileSystem();
            Assert.True(vfs.Mount(root).Success);

            var loader = new PermissionFileLoader(vfs, new PermissionTomlParser());
            var diagnostics = new PermissionDiagnostics();

            var files = loader.Load(diagnostics);

            Assert.Equal(2, files.Count);
            Assert.EndsWith("a.toml", files[0].Source);
            Assert.EndsWith("b.toml", files[1].Source);
            Assert.Empty(diagnostics.Errors);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class PermissionTomlParser : IFileParseService
    {
        public TResult Parse<TResult>(PathInfo path)
        {
            return (TResult)(object)Toml.To<PermissionFile>(path.ReadString());
        }

        public TResult Parse<TResult>(Stream stream)
        {
            throw new NotSupportedException();
        }

        public bool TryParse<TResult>(PathInfo path, out TResult result)
        {
            throw new NotSupportedException();
        }

        public bool TryParse<TResult>(Stream stream, out TResult result)
        {
            throw new NotSupportedException();
        }
    }
}
