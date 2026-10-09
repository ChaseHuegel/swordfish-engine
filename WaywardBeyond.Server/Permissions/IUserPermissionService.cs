using Swordfish.ECS;
using WaywardBeyond.Permissions;

namespace WaywardBeyond.Server.Permissions;

/// <summary>
///     Binds a joining client to its user claim and resolves permission checks for the session. A
///     connection with no binding is denied. The local host connection is always allowed.
/// </summary>
public interface IUserPermissionService
{
    /// <summary>Binds a client to its claim and caches the compiled permission set for the session.</summary>
    void Bind(Uuid clientId, in UserClaim claim, bool isHost);

    /// <summary>Removes a client's claim binding.</summary>
    void Unbind(Uuid clientId);

    /// <summary>Returns the bound claim for a client, if any.</summary>
    bool TryGetClaim(Uuid clientId, out UserClaim claim);

    /// <summary>True when the client is the local host connection.</summary>
    bool IsHost(Uuid clientId);

    /// <summary>Checks a permission for a bound client. An unbound client is denied.</summary>
    bool HasPermission(Uuid clientId, string permission);

    /// <summary>Returns the cached compiled set for a client, for callers that check many keys.</summary>
    bool TryGetPermissions(Uuid clientId, out PermissionSet permissions);
}
