using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using WaywardBeyond.Permissions;

namespace WaywardBeyond.Server.Permissions;

/// <summary>
///     Loads and compiles the permission policy at startup. It surfaces the loaded counts and every
///     load or compile diagnostic in the server log.
/// </summary>
public sealed class PermissionEntryPoint(PermissionPolicy policy, ILogger<PermissionEntryPoint> logger) : IEntryPoint
{
    /// <inheritdoc/>
    public void Run()
    {
        logger.LogInformation("Loaded permissions: {groups} groups, {users} users.", policy.GroupCount, policy.UserCount);

        foreach (string warning in policy.Diagnostics.Warnings)
        {
            logger.LogWarning("Permissions: {warning}", warning);
        }

        foreach (string error in policy.Diagnostics.Errors)
        {
            logger.LogError("Permissions: {error}", error);
        }
    }
}
