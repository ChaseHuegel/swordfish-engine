using WaywardBeyond.Permissions;

namespace WaywardBeyond.Server.Permissions;

/// <summary>
///     Resolves a peer's effective permission set. The local host connection bypasses the policy. This is
///     the one place the host rule lives, so menu-time and in-world checks cannot disagree.
/// </summary>
public sealed class PermissionResolver(PermissionPolicy policy)
{
    /// <summary>Returns the effective permission set for a claim. A host gets every permission.</summary>
    public PermissionSet Resolve(in UserClaim claim, bool isHost)
    {
        return isHost ? PermissionSet.AllowAll : policy.GetPermissions(claim.UserId);
    }
}
