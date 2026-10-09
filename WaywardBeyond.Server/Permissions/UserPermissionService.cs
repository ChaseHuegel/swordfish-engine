using System.Collections.Concurrent;
using Swordfish.ECS;
using WaywardBeyond.Permissions;

namespace WaywardBeyond.Server.Permissions;

/// <summary>
///     Per-world claim bindings. The service resolves each user's compiled set once at bind time, so a
///     check is one client lookup and one set lookup. The host connection uses the all-granting set.
/// </summary>
public sealed class UserPermissionService : IUserPermissionService
{
    private readonly IPermissionPolicy _policy;
    private readonly ConcurrentDictionary<Uuid, BoundClient> _clients = new();

    public UserPermissionService(in IPermissionPolicy policy)
    {
        _policy = policy;
    }

    public void Bind(Uuid clientId, in UserClaim claim, bool isHost)
    {
        PermissionSet permissions = isHost ? PermissionSet.AllowAll : _policy.GetPermissions(claim.UserId);
        _clients[clientId] = new BoundClient(claim, permissions, isHost);
    }

    public void Unbind(Uuid clientId)
    {
        _clients.TryRemove(clientId, out _);
    }

    public bool TryGetClaim(Uuid clientId, out UserClaim claim)
    {
        if (_clients.TryGetValue(clientId, out BoundClient? bound))
        {
            claim = bound.Claim;
            return true;
        }

        claim = UserClaim.Anonymous;
        return false;
    }

    public bool IsHost(Uuid clientId)
    {
        return _clients.TryGetValue(clientId, out BoundClient? bound) && bound.IsHost;
    }

    public bool HasPermission(Uuid clientId, string permission)
    {
        return _clients.TryGetValue(clientId, out BoundClient? bound) && bound.Permissions.HasPermission(permission);
    }

    public bool TryGetPermissions(Uuid clientId, out PermissionSet permissions)
    {
        if (_clients.TryGetValue(clientId, out BoundClient? bound))
        {
            permissions = bound.Permissions;
            return true;
        }

        permissions = null!;
        return false;
    }

    private sealed class BoundClient(in UserClaim claim, in PermissionSet permissions, bool isHost)
    {
        public readonly UserClaim Claim = claim;
        public readonly PermissionSet Permissions = permissions;
        public readonly bool IsHost = isHost;
    }
}
