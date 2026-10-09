using System;
using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Permissions;
using Xunit;

namespace Swordfish.Tests;

public class PermissionSetTests
{
    private static PermissionSet Compile(string toml)
    {
        var file = new SourcedPermissionFile("test.toml", Toml.To<PermissionFile>(toml));
        PermissionPolicy policy = PermissionPolicy.Create([file]);
        return policy.GetPermissions("nobody");
    }

    [Fact]
    public void AllowAllGrantsEveryKey()
    {
        Assert.True(PermissionSet.AllowAll.HasPermission("anything"));
        Assert.True(PermissionSet.AllowAll.HasPermission("deeply.nested.permission"));
    }

    [Fact]
    public void TrailingWildcardMatchesDescendantsOnly()
    {
        PermissionSet set = Compile("""
            [Groups.default]
            Permissions = ["a.*"]
            """);

        Assert.True(set.HasPermission("a.b"));
        Assert.True(set.HasPermission("a.b.c"));
        Assert.False(set.HasPermission("a"));
    }

    [Fact]
    public void GlobalWildcardMatchesEveryKey()
    {
        PermissionSet set = Compile("""
            [Groups.default]
            Permissions = ["*"]
            """);

        Assert.True(set.HasPermission("a"));
        Assert.True(set.HasPermission("a.b.c"));
    }

    [Fact]
    public void ExactMatchWinsOverWildcard()
    {
        PermissionSet set = Compile("""
            [Groups.default]
            Permissions = ["a.*", "-a.b"]
            """);

        Assert.False(set.HasPermission("a.b"));
        Assert.True(set.HasPermission("a.c"));
    }

    [Fact]
    public void HasPermissionAllocatesNothing()
    {
        PermissionSet set = Compile("""
            [Groups.default]
            Permissions = ["a.b", "a.*", "-a.b.c", "*"]

            [Users."user-1"]
            Permissions = ["-global.deny"]
            """);

        string[] keys = ["a.b", "a.c", "a.b.c", "global.deny", "other.key"];

        //  Warm every lookup branch before measuring.
        for (var i = 0; i < 1000; i++)
        {
            _ = set.HasPermission(keys[i % keys.Length]);
        }

        var hits = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            if (set.HasPermission(keys[i % keys.Length]))
            {
                hits++;
            }
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.True(hits > 0);
    }
}
