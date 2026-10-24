using System;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Permissions;

namespace WaywardBeyond.Client.Identity;

/// <summary>
///     Supplies the local user's claim from <c>profile.toml</c>.
///     The id is generated once and reused, so a server can associate the same user across sessions.
/// </summary>
internal sealed class ProfileUserClaimProvider(ProfileSettings profileSettings)
{
    public UserClaim GetClaim()
    {
        string userId = profileSettings.UserId.Get();
        if (string.IsNullOrEmpty(userId))
        {
            userId = Guid.NewGuid().ToString();
            profileSettings.UserId.Set(userId);
            profileSettings.Save();
        }

        return new UserClaim(userId);
    }
}
