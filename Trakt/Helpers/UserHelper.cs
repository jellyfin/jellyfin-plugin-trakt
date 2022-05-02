using System;
using System.Linq;
using Jellyfin.Data.Entities;
using Trakt.Model;

namespace Trakt.Helpers;

internal static class UserHelper
{
    public static TraktUser GetTraktUser(User user)
    {
        return GetTraktUser(user.Id);
    }

    public static TraktUser GetTraktUser(string userId)
    {
        return GetTraktUser(new Guid(userId));
    }

    public static TraktUser GetTraktUser(Guid userGuid)
    {
        var traktUsers = Plugin.Instance.PluginConfiguration.GetAllTraktUsers();
        if (traktUsers.Count == 0)
        {
            return null;
        }

        return traktUsers.FirstOrDefault(user =>
        {
            if (string.IsNullOrWhiteSpace(user.LinkedMbUserId))
            {
                return false;
            }

            if (Guid.TryParse(user.LinkedMbUserId, out Guid traktUserGuid)
                && traktUserGuid.Equals(userGuid))
            {
                return true;
            }

            return false;
        });
    }
}
