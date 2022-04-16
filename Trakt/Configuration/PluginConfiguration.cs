#pragma warning disable CA1819

using System;
using System.Linq;
using MediaBrowser.Model.Plugins;
using Trakt.Model;

namespace Trakt.Configuration
{
    /// <summary>
    /// Plugin configuration class for trackt.tv plugin.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
        /// </summary>
        public PluginConfiguration()
        {
            TraktUsers = Array.Empty<TraktUser>();
        }

        /// <summary>
        /// Gets or sets the trakt users.
        /// </summary>
        public TraktUser[] TraktUsers { get; set; }

        /// <summary>
        /// Adds a user to the trakt users.
        /// </summary>
        /// <param name="userId">The user id.</param>
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

        /// <summary>
        /// Removes a user from the trakt users.
        /// </summary>
        /// <param name="userId">The user id.</param>
        public void RemoveUser(string userId)
        {
            var traktUsers = TraktUsers.ToList();
            traktUsers.RemoveAll(user => user.LinkedMbUserId == userId);
            TraktUsers = traktUsers.ToArray();
        }
    }
}
