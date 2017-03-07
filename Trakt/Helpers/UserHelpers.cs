using System;
using System.Linq;
using MediaBrowser.Controller.Entities;
using Trakt.Model;

namespace Trakt.Helpers
{
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

                Guid traktUserGuid;
                if (Guid.TryParse(tUser.LinkedMbUserId, out traktUserGuid) && traktUserGuid.Equals(userGuid))
                {
                    return true;
                }

                return false;
            });
        }
    }
}
