using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Permissions;
using Xunit;

namespace Swordfish.Tests;

public class PermissionPolicyTests
{
    private static SourcedPermissionFile File(string toml)
    {
        return new SourcedPermissionFile("test.toml", Toml.To<PermissionFile>(toml));
    }

    [Fact]
    public void UnlistedUsersShareTheDefaultSet()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("""
                [Groups.default]
                Permissions = ["baseline"]
                """),
        ]);

        PermissionSet first = policy.GetPermissions("nobody-1");
        PermissionSet second = policy.GetPermissions("nobody-2");

        Assert.Same(policy.DefaultSet, first);
        Assert.Same(first, second);
    }

    [Fact]
    public void ListedUserWithoutGrantsSharesTheDefaultSet()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("""
                [Groups.default]
                Permissions = ["baseline"]

                [Users."user-1"]
                Roles = []
                """),
        ]);

        Assert.Same(policy.DefaultSet, policy.GetPermissions("user-1"));
    }

    [Fact]
    public void ListedUserGetsACompiledSet()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("""
                [Groups.default]
                Permissions = ["baseline"]

                [Users."user-1"]
                Permissions = ["personal"]
                """),
        ]);

        Assert.NotSame(policy.DefaultSet, policy.GetPermissions("user-1"));
        Assert.True(policy.HasPermission("user-1", "baseline"));
        Assert.True(policy.HasPermission("user-1", "personal"));
    }

    [Fact]
    public void DefaultGrantsApplyToUnlistedUsers()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("""
                [Groups.default]
                Permissions = ["baseline"]
                """),
        ]);

        Assert.True(policy.HasPermission("unknown-user", "baseline"));
        Assert.False(policy.HasPermission("unknown-user", "other"));
    }

    [Fact]
    public void CountsAndNamesReflectLoadedContent()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("""
                [Groups.default]
                [Groups.builder]

                [Users."user-1"]
                Roles = ["builder"]
                """),
        ]);

        Assert.Equal(2, policy.GroupCount);
        Assert.Equal(1, policy.UserCount);
        Assert.Contains("builder", policy.GroupNames);
        Assert.Contains("user-1", policy.UserNames);
    }
}
