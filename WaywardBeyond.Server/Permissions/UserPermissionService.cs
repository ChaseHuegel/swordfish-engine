using System.Collections.Concurrent;
using Swordfish.ECS;
using WaywardBeyond.Permissions;

namespace WaywardBeyond.Server.Permissions;

/// <summary>
///     Per-world claim bindings. The service resolves each user's compiled set once at bind time, so a
///     check is one client lookup and one set lookup. The host connection uses the all-granting set.
/// </summary>
public sealed class UserPermissionService(PermissionResolver resolver) : IUserPermissionService
{
    private readonly PermissionResolver _resolver = resolver;
    private readonly ConcurrentDictionary<Uuid, PermissionSet> _permissions = new();

    /// <inheritdoc/>
    public void Bind(Uuid clientId, in UserClaim claim, bool isHost)
    {
        _permissions[clientId] = _resolver.Resolve(in claim, isHost);
    }

    /// <inheritdoc/>
    public void Unbind(Uuid clientId)
    {
        _permissions.TryRemove(clientId, out _);
    }

    /// <inheritdoc/>
    public bool HasPermission(Uuid clientId, string permission)
    {
        return _permissions.TryGetValue(clientId, out PermissionSet? permissions) && permissions.HasPermission(permission);
    }
}
