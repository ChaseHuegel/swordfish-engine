using Swordfish.ECS;
using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Permissions;
using WaywardBeyond.Server.Permissions;
using Xunit;

namespace Swordfish.Tests;

public class UserPermissionServiceTests
{
    [Fact]
    public void UnboundClientIsDenied()
    {
        UserPermissionService service = CreateService();

        Assert.False(service.HasPermission(Uuid.FromValue(1), "default.key"));
    }

    [Fact]
    public void HostIsAlwaysAllowed()
    {
        UserPermissionService service = CreateService();
        Uuid clientId = Uuid.FromValue(1);

        service.Bind(clientId, UserClaim.Anonymous, isHost: true);

        Assert.True(service.HasPermission(clientId, "any.key"));
        Assert.True(service.HasPermission(clientId, "admin.key"));
    }

    [Fact]
    public void BoundClientUsesItsClaim()
    {
        UserPermissionService service = CreateService();
        Uuid clientId = Uuid.FromValue(1);

        service.Bind(clientId, new UserClaim("boss"), isHost: false);

        Assert.True(service.HasPermission(clientId, "admin.key"));
        Assert.True(service.HasPermission(clientId, "default.key"));
    }

    [Fact]
    public void UnknownClaimFallsBackToTheDefaultSet()
    {
        UserPermissionService service = CreateService();
        Uuid clientId = Uuid.FromValue(1);

        service.Bind(clientId, new UserClaim("nobody"), isHost: false);

        Assert.True(service.HasPermission(clientId, "default.key"));
        Assert.False(service.HasPermission(clientId, "admin.key"));
    }

    [Fact]
    public void UnbindRevokesAccess()
    {
        UserPermissionService service = CreateService();
        Uuid clientId = Uuid.FromValue(1);

        service.Bind(clientId, new UserClaim("boss"), isHost: false);
        service.Unbind(clientId);

        Assert.False(service.HasPermission(clientId, "admin.key"));
    }

    [Fact]
    public void BindReplacesThePreviousClaim()
    {
        UserPermissionService service = CreateService();
        Uuid clientId = Uuid.FromValue(1);

        service.Bind(clientId, new UserClaim("boss"), isHost: false);
        service.Bind(clientId, new UserClaim("nobody"), isHost: false);

        Assert.False(service.HasPermission(clientId, "admin.key"));
        Assert.True(service.HasPermission(clientId, "default.key"));
    }

    private static UserPermissionService CreateService()
    {
        var file = new SourcedPermissionFile("test.toml", Toml.To<PermissionFile>("""
            [Groups.default]
            Permissions = ["default.key"]

            [Groups.admin]
            Permissions = ["admin.key"]

            [Users."boss"]
            Roles = ["admin"]
            """));
        var policy = PermissionPolicy.Create([file]);
        return new UserPermissionService(new PermissionResolver(policy));
    }
}
