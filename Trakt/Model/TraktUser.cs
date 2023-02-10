#pragma warning disable CA1819

using System;

namespace Trakt.Model;

/// <summary>
/// Trakt.tv user class.
/// </summary>
public class TraktUser
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TraktUser"/> class.
    /// </summary>
    public TraktUser()
    {
        AccessToken = null;
        RefreshToken = null;
        LinkedMbUserId = Guid.Empty;
        SkipUnwatchedImportFromTrakt = true;
        SkipWatchedImportFromTrakt = false;
        SkipPlaybackProgressImportFromTrakt = false;
        PostWatchedHistory = true;
        PostUnwatchedHistory = false;
        PostSetWatched = true;
        PostSetUnwatched = false;
        ExtraLogging = false;
        ExportMediaInfo = true;
        SynchronizeCollections = true;
        Scrobble = true;
        LocationsExcluded = null;
        AccessTokenExpiration = DateTime.MinValue;
        DontRemoveItemFromTrakt = true;
    }

    /// <summary>
    /// Gets or sets the access token.
    /// </summary>
    public string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    public string RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the linked Mb user id.
    /// </summary>
    public Guid LinkedMbUserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the skip unwatched import option is enabled or not.
    /// </summary>
    public bool SkipUnwatchedImportFromTrakt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the skip playback progress import option is enabled or not.
    /// </summary>
    public bool SkipPlaybackProgressImportFromTrakt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the skip watched import option is enabled or not.
    /// </summary>
    public bool SkipWatchedImportFromTrakt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the watch history should be posted or not.
    /// </summary>
    public bool PostWatchedHistory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the unwatch history should be posted or not.
    /// </summary>
    public bool PostUnwatchedHistory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether setting an item to watched should be posted or not.
    /// </summary>
    public bool PostSetWatched { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the setting an item to unwatched should be posted or not.
    /// </summary>
    public bool PostSetUnwatched { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether extra logging is enabled or not.
    /// </summary>
    public bool ExtraLogging { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the media info should be exported or not.
    /// </summary>
    public bool ExportMediaInfo { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether collections should be synchronized or not.
    /// </summary>
    public bool SynchronizeCollections { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether scrobbling should take place or not.
    /// </summary>
    public bool Scrobble { get; set; }

    /// <summary>
    /// Gets or sets the access token.
    /// </summary>
    public string[] LocationsExcluded { get; set; }

    /// <summary>
    /// Gets or sets the access token expiration.
    /// </summary>
    public DateTime AccessTokenExpiration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether item should be removed from trakt.tv.
    /// </summary>
    public bool DontRemoveItemFromTrakt { get; set; }
}
