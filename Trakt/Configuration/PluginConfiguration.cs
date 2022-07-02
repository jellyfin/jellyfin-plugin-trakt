#pragma warning disable CA1819

using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Plugins;
using Trakt.Model;

namespace Trakt.Configuration;

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
    /// Adds a user to the trakt.tv users.
    /// </summary>
    /// <param name="userGuid">The user Guid.</param>
    public void AddUser(Guid userGuid)
    {
        var traktUsers = TraktUsers.ToList();
        var traktUser = new TraktUser
        {
            LinkedMbUserId = userGuid
        };
        traktUsers.Add(traktUser);
        TraktUsers = traktUsers.ToArray();
    }

    /// <summary>
    /// Removes a user from the trakt users.
    /// </summary>
    /// <param name="userGuid">The user id.</param>
    public void RemoveUser(Guid userGuid)
    {
        var traktUsers = TraktUsers.ToList();
        traktUsers.RemoveAll(user => user.LinkedMbUserId == userGuid);
        TraktUsers = traktUsers.ToArray();
    }

    /// <summary>
    /// Gets a list of all trakt.tv users.
    /// </summary>
    /// <returns>IReadonlyList{TraktUser} with all trakt users.</returns>
    public IReadOnlyList<TraktUser> GetAllTraktUsers()
    {
        return TraktUsers.ToList();
    }
}
