using System;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using Trakt.Model;

namespace Trakt.Helpers;

internal static class UserHelper
{
    public static TraktUser GetTraktUser(string userId, bool authorized = false)
    {
        return GetTraktUser(Guid.Parse(userId), authorized);
    }

    public static TraktUser GetTraktUser(User user, bool authorized = false)
    {
        return GetTraktUser(user.Id, authorized);
    }

    public static TraktUser GetTraktUser(Guid userGuid, bool authorized = false)
    {
        var traktUsers = Plugin.Instance.PluginConfiguration.GetAllTraktUsers();
        if (traktUsers.Count == 0)
        {
            return null;
        }

        return traktUsers.FirstOrDefault(user =>
        {
            if (user.LinkedMbUserId == Guid.Empty
                || (authorized && string.IsNullOrWhiteSpace(user.AccessToken)))
            {
                return false;
            }

            if (user.LinkedMbUserId.Equals(userGuid))
            {
                return true;
            }

            return false;
        });
    }
}
