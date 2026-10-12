using System.Collections.Concurrent;
using Swordfish.ECS;
using WaywardBeyond.Permissions;

namespace WaywardBeyond.Server.Permissions;

/// <summary>
///     Per-world claim bindings. The service resolves each user's compiled set once at bind time, so a
///     check is one client lookup and one set lookup. The host connection uses the all-granting set.
/// </summary>
public sealed class UserPermissionService(PermissionPolicy policy) : IUserPermissionService
{
    private readonly ConcurrentDictionary<Uuid, PermissionSet> _permissions = new();

    public void Bind(Uuid clientId, in UserClaim claim, bool isHost)
    {
        _permissions[clientId] = isHost ? PermissionSet.AllowAll : policy.GetPermissions(claim.UserId);
    }

    public void Unbind(Uuid clientId)
    {
        _permissions.TryRemove(clientId, out _);
    }

    public bool HasPermission(Uuid clientId, string permission)
    {
        return _permissions.TryGetValue(clientId, out PermissionSet? permissions) && permissions.HasPermission(permission);
    }
}