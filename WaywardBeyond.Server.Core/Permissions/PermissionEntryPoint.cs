using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using WaywardBeyond.Shared.Permissions;

namespace WaywardBeyond.Server.Core.Permissions;

/// <summary>
///     Loads and compiles the permission policy at startup. It surfaces the loaded counts and every
///     load or compile diagnostic in the server log.
/// </summary>
public sealed class PermissionEntryPoint : IEntryPoint
{
    private readonly IPermissionPolicy _policy;
    private readonly ILogger<PermissionEntryPoint> _logger;

    public PermissionEntryPoint(in IPermissionPolicy policy, in ILogger<PermissionEntryPoint> logger)
    {
        _policy = policy;
        _logger = logger;
    }

    public void Run()
    {
        _logger.LogInformation("Loaded permissions: {groups} groups, {users} users.", _policy.GroupCount, _policy.UserCount);

        foreach (string warning in _policy.Diagnostics.Warnings)
        {
            _logger.LogWarning("Permissions: {warning}", warning);
        }

        foreach (string error in _policy.Diagnostics.Errors)
        {
            _logger.LogError("Permissions: {error}", error);
        }
    }
}
