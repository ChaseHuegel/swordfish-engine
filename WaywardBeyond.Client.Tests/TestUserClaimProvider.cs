using WaywardBeyond.Permissions;

namespace WaywardBeyond.Client.Tests;

/// <summary>A fixed claim for tests that need the identity seam without profile settings.</summary>
internal sealed class TestUserClaimProvider : IUserClaimProvider
{
    public UserClaim GetClaim() => new("test-user");
}
