#pragma warning disable CA1819

using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Plugins;
using Trakt.Model;

namespace Trakt.Configuration;

/// <summary>
/// Plugin configuration class for trakt.tv plugin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        TraktUsers = Array.Empty<TraktUser>();
        DefaultAllowExternalTokenAccess = false;
    }

    /// <summary>
    /// Gets or sets the trakt users.
    /// </summary>
    public TraktUser[] TraktUsers { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether newly created Trakt users allow token export by default.
    /// Administrators can still override this per user.
    /// </summary>
    public bool DefaultAllowExternalTokenAccess { get; set; }

    /// <summary>
    /// Adds a user to the trakt.tv users.
    /// </summary>
    /// <param name="userGuid">The user Guid.</param>
    public void AddUser(Guid userGuid)
    {
        var traktUsers = TraktUsers.ToList();
        var traktUser = new TraktUser
        {
            LinkedMbUserId = userGuid,
            AllowExternalTokenAccess = DefaultAllowExternalTokenAccess
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
