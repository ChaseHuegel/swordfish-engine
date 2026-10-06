namespace WaywardBeyond.Shared.Permissions;

/// <summary>
///     Supplies the local user's claim. The client populates the claim from its profile. A future
///     account system replaces the implementation without changing consumers.
/// </summary>
public interface IUserClaimProvider
{
    UserClaim GetClaim();
}
