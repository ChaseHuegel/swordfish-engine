using System.Collections.Generic;
using System.Linq;
using System.Text;
using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Shared.Permissions;
using Xunit;

namespace Swordfish.Tests;

public class PermissionCompilerTests
{
    private static SourcedPermissionFile File(string source, string toml)
    {
        return new SourcedPermissionFile(source, Toml.To<PermissionFile>(toml));
    }

    [Fact]
    public void MergesSameGroupAcrossFiles()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("01-builder.toml", """
                [Groups.builder]
                Permissions = ["waywardbeyond.brick.place"]

                [Users."user-1"]
                Roles = ["builder"]
                """),
            File("02-builder-extra.toml", """
                [Groups.builder]
                Permissions = ["waywardbeyond.brick.break"]
                """),
        ]);

        Assert.True(policy.HasPermission("user-1", "waywardbeyond.brick.place"));
        Assert.True(policy.HasPermission("user-1", "waywardbeyond.brick.break"));
    }

    [Fact]
    public void ResolvesInheritanceTransitively()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("groups.toml", """
                [Groups.default]
                Permissions = ["baseline"]

                [Groups.builder]
                Inherits = ["default"]
                Permissions = ["build"]

                [Groups.moderator]
                Inherits = ["builder"]
                Permissions = ["moderate"]

                [Users."staff"]
                Roles = ["moderator"]
                """),
        ]);

        Assert.True(policy.HasPermission("staff", "baseline"));
        Assert.True(policy.HasPermission("staff", "build"));
        Assert.True(policy.HasPermission("staff", "moderate"));
        Assert.False(policy.HasPermission("staff", "unrelated"));
    }

    [Fact]
    public void DefaultGroupAppliesToEveryUser()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("groups.toml", """
                [Groups.default]
                Permissions = ["baseline"]

                [Groups.builder]
                Permissions = ["build"]

                [Users."builder-user"]
                Roles = ["builder"]
                """),
        ]);

        Assert.True(policy.HasPermission("builder-user", "baseline"));
        Assert.True(policy.HasPermission("unlisted-user", "baseline"));
        Assert.False(policy.HasPermission("unlisted-user", "build"));
    }

    [Fact]
    public void DenyWinsOverGrantAtTheSameNode()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("groups.toml", """
                [Groups.default]
                Permissions = ["a.b"]

                [Users."user-1"]
                Permissions = ["-a.b"]
                """),
        ]);

        Assert.False(policy.HasPermission("user-1", "a.b"));
    }

    [Fact]
    public void DenyWinsRegardlessOfDeclarationOrder()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("groups.toml", """
                [Groups.default]
                Permissions = ["-a.b", "a.b"]
                """),
        ]);

        Assert.False(policy.HasPermission("user-1", "a.b"));
    }

    [Fact]
    public void ExactNodeOverridesWildcard()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("groups.toml", """
                [Groups.default]
                Permissions = ["a.*"]

                [Users."user-1"]
                Permissions = ["-a.b"]
                """),
        ]);

        Assert.False(policy.HasPermission("user-1", "a.b"));
        Assert.True(policy.HasPermission("user-1", "a.c"));
    }

    [Fact]
    public void LongestWildcardPrefixWins()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("groups.toml", """
                [Groups.default]
                Permissions = ["a.*"]

                [Users."user-1"]
                Permissions = ["-a.b.*"]
                """),
        ]);

        Assert.False(policy.HasPermission("user-1", "a.b.c"));
        Assert.True(policy.HasPermission("user-1", "a.c"));
    }

    [Fact]
    public void TrailingWildcardDoesNotGrantItsOwnPrefix()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("groups.toml", """
                [Groups.default]
                Permissions = ["a.*"]
                """),
        ]);

        Assert.True(policy.HasPermission("user-1", "a.b"));
        Assert.False(policy.HasPermission("user-1", "a"));
    }

    [Fact]
    public void GlobalWildcardGrantsEverything()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("groups.toml", """
                [Groups.default]
                Permissions = ["*"]
                """),
        ]);

        Assert.True(policy.HasPermission("user-1", "anything.at.all"));
        Assert.True(policy.HasPermission("user-1", "*"));
    }

    [Fact]
    public void GlobalDenyWinsOverGlobalGrant()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("groups.toml", """
                [Groups.default]
                Permissions = ["*", "-*"]
                """),
        ]);

        Assert.False(policy.HasPermission("user-1", "anything.at.all"));
    }

    [Fact]
    public void SelfCycleIsBrokenAndReported()
    {
        var diagnostics = new PermissionDiagnostics();
        PermissionPolicy policy = PermissionPolicy.Create([
            File("cycle.toml", """
                [Groups.loop]
                Inherits = ["loop"]
                Permissions = ["self.grant"]

                [Users."user-1"]
                Roles = ["loop"]
                """),
        ], diagnostics);

        Assert.True(policy.HasPermission("user-1", "self.grant"));
        Assert.Contains(diagnostics.Warnings, warning => warning.Contains("Inheritance cycle"));
    }

    [Fact]
    public void MutualCycleAcrossFilesIsBrokenAndReportedOnce()
    {
        var diagnostics = new PermissionDiagnostics();
        PermissionPolicy policy = PermissionPolicy.Create([
            File("alpha.toml", """
                [Groups.alpha]
                Inherits = ["beta"]
                Permissions = ["alpha.grant"]

                [Users."user-1"]
                Roles = ["alpha"]
                """),
            File("beta.toml", """
                [Groups.beta]
                Inherits = ["alpha"]
                Permissions = ["beta.grant"]
                """),
        ], diagnostics);

        Assert.True(policy.HasPermission("user-1", "alpha.grant"));
        Assert.True(policy.HasPermission("user-1", "beta.grant"));
        Assert.Equal(1, diagnostics.Warnings.Count(warning => warning.Contains("Inheritance cycle")));
        Assert.Contains("alpha.toml", string.Join(" ", diagnostics.Warnings));
        Assert.Contains("beta.toml", string.Join(" ", diagnostics.Warnings));
    }

    [Fact]
    public void DiamondInheritanceIsNotACycle()
    {
        var diagnostics = new PermissionDiagnostics();
        PermissionPolicy policy = PermissionPolicy.Create([
            File("groups.toml", """
                [Groups.base]
                Permissions = ["base.grant"]

                [Groups.left]
                Inherits = ["base"]
                Permissions = ["left.grant"]

                [Groups.right]
                Inherits = ["base"]
                Permissions = ["right.grant"]

                [Groups.top]
                Inherits = ["left", "right"]

                [Users."user-1"]
                Roles = ["top"]
                """),
        ], diagnostics);

        Assert.True(policy.HasPermission("user-1", "base.grant"));
        Assert.True(policy.HasPermission("user-1", "left.grant"));
        Assert.True(policy.HasPermission("user-1", "right.grant"));
        Assert.DoesNotContain(diagnostics.Warnings, warning => warning.Contains("Inheritance cycle"));
    }

    [Fact]
    public void DeepInheritanceIsTruncatedWithoutHanging()
    {
        var file = new PermissionFile();
        for (var i = 0; i < 70; i++)
        {
            file.Groups[$"g{i}"] = new PermissionGroupGrant
            {
                Inherits = i < 69 ? [$"g{i + 1}"] : [],
                Permissions = i == 69 ? ["deep.grant"] : [],
            };
        }

        file.Users["user-1"] = new PermissionUserGrant { Roles = ["g0"] };

        var diagnostics = new PermissionDiagnostics();
        PermissionPolicy policy = PermissionPolicy.Create([new SourcedPermissionFile("deep.toml", file)], diagnostics);

        Assert.Contains(diagnostics.Errors, error => error.Contains("maximum depth"));
        Assert.False(policy.HasPermission("user-1", "deep.grant"));
    }

    [Fact]
    public void InvalidNodesAreIgnoredWithWarnings()
    {
        var diagnostics = new PermissionDiagnostics();
        PermissionPolicy policy = PermissionPolicy.Create([
            File("invalid.toml", """
                [Groups.default]
                Permissions = ["a..b", ".leading", "trailing.", "-", "middle.*.wildcard", "good.node"]
                """),
        ], diagnostics);

        Assert.True(policy.HasPermission("user-1", "good.node"));
        Assert.False(policy.HasPermission("user-1", "a.b"));
        Assert.True(diagnostics.Warnings.Count(warning => warning.Contains("Ignoring invalid permission node")) >= 5);
    }

    [Fact]
    public void UnknownRoleAndParentWarn()
    {
        var diagnostics = new PermissionDiagnostics();
        PermissionPolicy policy = PermissionPolicy.Create([
            File("unknown.toml", """
                [Groups.builder]
                Inherits = ["missing-parent"]

                [Users."user-1"]
                Roles = ["builder", "missing-role"]
                """),
        ], diagnostics);

        Assert.Contains(diagnostics.Warnings, warning => warning.Contains("missing-parent"));
        Assert.Contains(diagnostics.Warnings, warning => warning.Contains("missing-role"));
    }

    [Fact]
    public void NamesAndNodesAreCaseInsensitive()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("case.toml", """
                [Groups.Builder]
                Permissions = ["WaywardBeyond.Brick.Place"]

                [Users."User-1"]
                Roles = ["builder"]
                """),
        ]);

        Assert.True(policy.HasPermission("user-1", "waywardbeyond.brick.place"));
    }

    [Fact]
    public void UserRolesStackTogether()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("roles.toml", """
                [Groups.builder]
                Permissions = ["build"]

                [Groups.worker]
                Permissions = ["work"]

                [Users."user-1"]
                Roles = ["builder", "worker"]
                Permissions = ["personal"]
                """),
        ]);

        Assert.True(policy.HasPermission("user-1", "build"));
        Assert.True(policy.HasPermission("user-1", "work"));
        Assert.True(policy.HasPermission("user-1", "personal"));
    }

    [Fact]
    public void EmptyInputCompilesToAnEmptyPolicy()
    {
        var diagnostics = new PermissionDiagnostics();
        PermissionPolicy policy = PermissionPolicy.Create([], diagnostics);

        Assert.False(diagnostics.HasIssues);
        Assert.False(policy.HasPermission("user-1", "anything"));
        Assert.False(policy.HasPermission("", "anything"));
    }

    [Fact]
    public void DeepChainUnderTheCapResolvesFully()
    {
        //  A chain under the cap resolves fully; the cap test above proves truncation.
        var builders = new StringBuilder();
        for (var i = 0; i < 32; i++)
        {
            builders.AppendLine($"[Groups.g{i}]");
            builders.AppendLine(i < 31 ? $"Inherits = [\"g{i + 1}\"]" : "Permissions = [\"chain.grant\"]");
            builders.AppendLine();
        }

        builders.AppendLine("[Users.\"user-1\"]");
        builders.AppendLine("Roles = [\"g0\"]");

        var diagnostics = new PermissionDiagnostics();
        PermissionPolicy policy = PermissionPolicy.Create([File("chain.toml", builders.ToString())], diagnostics);

        Assert.True(policy.HasPermission("user-1", "chain.grant"));
        Assert.Empty(diagnostics.Errors);
    }

    [Fact]
    public void ListedUserCanInspectRolesAndEffectivePermissions()
    {
        PermissionPolicy policy = PermissionPolicy.Create([
            File("groups.toml", """
                [Groups.default]
                Permissions = ["baseline"]

                [Groups.builder]
                Inherits = ["default"]
                Permissions = ["build"]

                [Users."user-1"]
                Roles = ["builder"]
                Permissions = ["personal"]
                """),
        ]);

        IReadOnlyCollection<string> roles = policy.GetRoles("user-1");
        IReadOnlyCollection<string> effective = policy.GetEffectivePermissions("user-1");

        Assert.Contains("builder", roles);
        Assert.Contains("default", roles);
        Assert.Contains("baseline", effective);
        Assert.Contains("build", effective);
        Assert.Contains("personal", effective);
    }
}
