using System.Collections.Generic;

namespace WaywardBeyond.Shared.Permissions;

/// <summary>
///     Resolves effective permissions for a user id. A policy is immutable after startup, so any thread
///     can query it without locking.
/// </summary>
public interface IPermissionPolicy
{
    /// <summary>Returns the compiled permission set for a user. Unlisted users share the default set.</summary>
    PermissionSet GetPermissions(string userId);

    /// <summary>Convenience check. Prefer <see cref="GetPermissions"/> when checking repeatedly.</summary>
    bool HasPermission(string userId, string permission);

    /// <summary>Returns the roles that apply to the user, including inherited and default roles.</summary>
    IReadOnlyCollection<string> GetRoles(string userId);

    /// <summary>Returns the declared permission nodes granted to the user, for diagnostics.</summary>
    IReadOnlyCollection<string> GetEffectivePermissions(string userId);
}
