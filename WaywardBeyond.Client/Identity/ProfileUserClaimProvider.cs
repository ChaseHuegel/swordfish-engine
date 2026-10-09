using System;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Permissions;

namespace WaywardBeyond.Client.Identity;

/// <summary>
///     Supplies the local user's claim from <c>profile.toml</c>. The id is generated once and reused,
///     so a server can associate the same user across sessions.
/// </summary>
internal sealed class ProfileUserClaimProvider : IUserClaimProvider
{
    private readonly ProfileSettings _profileSettings;

    public ProfileUserClaimProvider(in ProfileSettings profileSettings)
    {
        _profileSettings = profileSettings;
    }

    public UserClaim GetClaim()
    {
        string userId = _profileSettings.UserId.Get();
        if (string.IsNullOrEmpty(userId))
        {
            userId = Guid.NewGuid().ToString();
            _profileSettings.UserId.Set(userId);
            _profileSettings.Save();
        }

        return new UserClaim(userId);
    }
}
