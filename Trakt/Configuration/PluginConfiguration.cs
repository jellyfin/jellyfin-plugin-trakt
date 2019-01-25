using System.Linq;
using MediaBrowser.Model.Plugins;
using Trakt.Model;

namespace Trakt.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            TraktUsers = new TraktUser[] {};
        }

        public TraktUser[] TraktUsers { get; set; }

        public void AddUser(string userId)
        {
            var traktUsers = TraktUsers.ToList();
            var traktUser = new TraktUser
            {
                LinkedMbUserId = userId
            };
            traktUsers.Add(traktUser);
            TraktUsers = traktUsers.ToArray();
        }
    }
}
