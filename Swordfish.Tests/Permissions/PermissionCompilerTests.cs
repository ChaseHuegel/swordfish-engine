using System.Text;
using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Permissions;
using Xunit;

namespace Swordfish.Tests;

public class PermissionCompilerTests
{
    [Fact]
    public void SameNamedGroupsMergeAcrossFiles()
    {
        PermissionPolicy policy = Compile(
            ("one.toml", """
                [Groups.g]
                Permissions = ["a"]
                """),
            ("two.toml", """
                [Groups.g]
                Permissions = ["b"]

                [Users."u"]
                Roles = ["g"]
                """));

        Assert.True(policy.HasPermission("u", "a"));
        Assert.True(policy.HasPermission("u", "b"));
    }

    [Fact]
    public void DenyWinsWithinOneLayer()
    {
        PermissionPolicy policy = Compile(
            ("one.toml", """
                [Groups.g]
                Permissions = ["k"]
                """),
            ("two.toml", """
                [Groups.g]
                Permissions = ["-k"]

                [Users."u"]
                Roles = ["g"]
                """));

        Assert.False(policy.HasPermission("u", "k"));
    }

    [Fact]
    public void UserGrantOverridesGroupDeny()
    {
        PermissionPolicy policy = Compile(("a.toml", """
            [Groups.default]
            Permissions = ["-x.y"]

            [Users."u"]
            Permissions = ["x.y"]
            """));

        Assert.True(policy.HasPermission("u", "x.y"));
        Assert.False(policy.HasPermission("nobody", "x.y"));
    }

    [Fact]
    public void UserDenyOverridesGroupGrant()
    {
        PermissionPolicy policy = Compile(("a.toml", """
            [Groups.default]
            Permissions = ["x.y"]

            [Users."u"]
            Permissions = ["-x.y"]
            """));

        Assert.False(policy.HasPermission("u", "x.y"));
        Assert.True(policy.HasPermission("nobody", "x.y"));
    }

    [Fact]
    public void UserWildcardGrantOverridesGroupExactDeny()
    {
        PermissionPolicy policy = Compile(("a.toml", """
            [Groups.default]
            Permissions = ["-a.b"]

            [Users."u"]
            Permissions = ["a.*"]
            """));

        Assert.True(policy.HasPermission("u", "a.b"));
        Assert.True(policy.HasPermission("u", "a.c"));
        Assert.False(policy.HasPermission("nobody", "a.b"));
    }

    [Fact]
    public void UserWildcardDenyOverridesGroupExactGrant()
    {
        PermissionPolicy policy = Compile(("a.toml", """
            [Groups.default]
            Permissions = ["a.b"]

            [Users."u"]
            Permissions = ["-a.*"]
            """));

        Assert.False(policy.HasPermission("u", "a.b"));
        Assert.True(policy.HasPermission("nobody", "a.b"));
    }

    [Fact]
    public void EveryUserBelongsToTheDefaultGroup()
    {
        PermissionPolicy policy = Compile(("a.toml", """
            [Groups.default]
            Permissions = ["shared"]

            [Groups.extra]
            Permissions = ["extra"]
            """));

        Assert.True(policy.HasPermission("nobody", "shared"));
        Assert.False(policy.HasPermission("nobody", "extra"));
    }

    [Fact]
    public void UnlistedUsersShareTheDefaultSet()
    {
        PermissionPolicy policy = Compile(("a.toml", """
            [Groups.default]
            Permissions = ["k"]
            """));

        Assert.Same(policy.GetPermissions("nobody"), policy.GetPermissions("also-nobody"));
    }

    [Fact]
    public void InheritanceCycleKeepsAcyclicEdgesAndWarnsOnce()
    {
        PermissionPolicy policy = Compile(("a.toml", """
            [Groups.default]
            Inherits = ["a"]

            [Groups.a]
            Inherits = ["b"]
            Permissions = ["p.a"]

            [Groups.b]
            Inherits = ["a"]
            Permissions = ["p.b"]

            [Users."u1"]
            Roles = ["a"]

            [Users."u2"]
            Roles = ["a"]
            """));

        Assert.True(policy.HasPermission("u1", "p.a"));
        Assert.True(policy.HasPermission("u1", "p.b"));
        string warning = Assert.Single(policy.Diagnostics.Warnings);
        Assert.Contains("a -> b -> a", warning);
    }

    [Fact]
    public void InheritanceDepthCapTruncatesWithOneError()
    {
        var toml = new StringBuilder();
        for (var i = 0; i < 70; i++)
        {
            toml.AppendLine($"[Groups.g{i}]");
            if (i < 69)
            {
                toml.AppendLine($"Inherits = [\"g{i + 1}\"]");
            }

            toml.AppendLine($"Permissions = [\"k{i}\"]");
        }

        toml.AppendLine("[Groups.default]");
        toml.AppendLine("Inherits = [\"g0\"]");

        PermissionPolicy policy = Compile(("deep.toml", toml.ToString()));

        Assert.True(policy.HasPermission("nobody", "k0"));
        Assert.False(policy.HasPermission("nobody", "k69"));
        Assert.Single(policy.Diagnostics.Errors);
    }

    [Fact]
    public void GroupCapTruncatesWithOneError()
    {
        var toml = new StringBuilder();
        toml.AppendLine("[Groups.default]");
        toml.Append("Inherits = [");
        for (var i = 0; i < 400; i++)
        {
            if (i > 0)
            {
                toml.Append(", ");
            }

            toml.Append($"\"f{i}\"");
        }

        toml.AppendLine("]");

        for (var i = 0; i < 400; i++)
        {
            toml.AppendLine($"[Groups.f{i}]");
            toml.AppendLine($"Permissions = [\"k{i}\"]");
        }

        PermissionPolicy policy = Compile(("fan.toml", toml.ToString()));

        Assert.True(policy.HasPermission("nobody", "k0"));
        Assert.Single(policy.Diagnostics.Errors);
    }

    [Fact]
    public void InvalidNodeWarnsOncePerDeclaration()
    {
        PermissionPolicy policy = Compile(("a.toml", """
            [Groups.default]
            Permissions = ["a.*.b"]

            [Users."u1"]
            Roles = ["default"]

            [Users."u2"]
            Roles = ["default"]
            """));

        string warning = Assert.Single(policy.Diagnostics.Warnings);
        Assert.Contains("a.*.b", warning);
    }

    [Fact]
    public void InvalidNodeInUnusedGroupWarns()
    {
        PermissionPolicy policy = Compile(("a.toml", """
            [Groups.unused]
            Permissions = ["a.*.b"]
            """));

        string warning = Assert.Single(policy.Diagnostics.Warnings);
        Assert.Contains("a.*.b", warning);
    }

    [Fact]
    public void DenyDoesNotApplyToDescendants()
    {
        PermissionPolicy policy = Compile(("a.toml", """
            [Groups.default]
            Permissions = ["a.*", "-a.b"]
            """));

        Assert.False(policy.HasPermission("nobody", "a.b"));
        Assert.True(policy.HasPermission("nobody", "a.b.c"));
    }

    private static PermissionPolicy Compile(params (string Source, string Toml)[] files)
    {
        var sourced = new SourcedPermissionFile[files.Length];
        for (var i = 0; i < files.Length; i++)
        {
            sourced[i] = new SourcedPermissionFile(files[i].Source, Toml.To<PermissionFile>(files[i].Toml));
        }

        return PermissionPolicy.Create(sourced);
    }
}
