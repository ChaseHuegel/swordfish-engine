using Swordfish.ECS;
using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Server.Core.Permissions;
using WaywardBeyond.Shared.Permissions;
using Xunit;

namespace Swordfish.Tests;

public class UserPermissionServiceTests
{
    private const string SAVE = "waywardbeyond.level.save";

    private static UserPermissionService CreateService()
    {
        var file = new SourcedPermissionFile("test.toml", Toml.To<PermissionFile>("""
            [Groups.default]
            Permissions = ["baseline"]

            [Groups.admin]
            Permissions = ["waywardbeyond.*"]

            [Users."admin-user"]
            Roles = ["admin"]
            """));
        return new UserPermissionService(PermissionPolicy.Create([file]));
    }

    [Fact]
    public void UnboundClientIsDenied()
    {
        UserPermissionService service = CreateService();
        var clientId = Uuid.FromValue(1);

        Assert.False(service.HasPermission(clientId, SAVE));
        Assert.False(service.IsHost(clientId));
        Assert.False(service.TryGetClaim(clientId, out _));
        Assert.False(service.TryGetPermissions(clientId, out _));
    }

    [Fact]
    public void BoundRemoteUserGetsConfiguredPermissions()
    {
        UserPermissionService service = CreateService();
        var clientId = Uuid.FromValue(1);

        service.Bind(clientId, new UserClaim("admin-user"), isHost: false);

        Assert.True(service.TryGetClaim(clientId, out UserClaim claim));
        Assert.Equal("admin-user", claim.UserId);
        Assert.False(service.IsHost(clientId));
        Assert.True(service.HasPermission(clientId, SAVE));
    }

    [Fact]
    public void UnlistedUserGetsDefaultPermissionsOnly()
    {
        UserPermissionService service = CreateService();
        var clientId = Uuid.FromValue(1);

        service.Bind(clientId, new UserClaim("nobody"), isHost: false);

        Assert.True(service.HasPermission(clientId, "baseline"));
        Assert.False(service.HasPermission(clientId, SAVE));
    }

    [Fact]
    public void HostIsAllowedEverything()
    {
        UserPermissionService service = CreateService();
        var clientId = Uuid.FromValue(1);

        service.Bind(clientId, UserClaim.Anonymous, isHost: true);

        Assert.True(service.IsHost(clientId));
        Assert.True(service.HasPermission(clientId, SAVE));
        Assert.True(service.HasPermission(clientId, "any.other.key"));
    }

    [Fact]
    public void UnbindRemovesTheClaim()
    {
        UserPermissionService service = CreateService();
        var clientId = Uuid.FromValue(1);
        service.Bind(clientId, new UserClaim("admin-user"), isHost: false);

        service.Unbind(clientId);

        Assert.False(service.TryGetClaim(clientId, out _));
        Assert.False(service.HasPermission(clientId, SAVE));
    }

    [Fact]
    public void TryGetPermissionsReturnsTheCompiledSet()
    {
        UserPermissionService service = CreateService();
        var clientId = Uuid.FromValue(1);
        service.Bind(clientId, new UserClaim("admin-user"), isHost: false);

        Assert.True(service.TryGetPermissions(clientId, out PermissionSet permissions));
        Assert.True(permissions.HasPermission(SAVE));
    }
}
