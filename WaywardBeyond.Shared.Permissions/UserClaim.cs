namespace WaywardBeyond.Shared.Permissions;

/// <summary>
///     The identity a client presents when it joins a server. The user id is a stable string that the
///     client generates and persists in its profile.
/// </summary>
public readonly record struct UserClaim(string UserId)
{
    /// <summary>An absent claim. It resolves to the default permission set.</summary>
    public static readonly UserClaim Anonymous = new(string.Empty);

    public bool IsAnonymous => string.IsNullOrEmpty(UserId);
}
