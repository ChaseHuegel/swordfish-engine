using WaywardBeyond.Shared.Permissions;

namespace Swordfish.Tests;

internal static class TestPermissions
{
    /// <summary>An empty policy for fixtures that need the permission graph but load no files.</summary>
    public static IPermissionPolicy EmptyPolicy { get; } = PermissionPolicy.Create([]);
}
