using System;
using System.Linq;
using Jellyfin.Data.Entities;
using MediaBrowser.Controller.Entities;
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
        if (Plugin.Instance.PluginConfiguration.TraktUsers == null)
        {
            return null;
        }

        return Plugin.Instance.PluginConfiguration.TraktUsers.FirstOrDefault(tUser =>
        {
            if (string.IsNullOrWhiteSpace(tUser.LinkedMbUserId))
            {
                return false;
            }

            if (Guid.TryParse(tUser.LinkedMbUserId, out Guid traktUserGuid)
                && traktUserGuid.Equals(userGuid))
            {
                return true;
            }

            return false;
        });
    }
}
