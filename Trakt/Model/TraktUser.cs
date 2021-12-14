#pragma warning disable CA1819

using System;

namespace Trakt.Model;

public class TraktUser
{
    public TraktUser()
    {
        SkipUnwatchedImportFromTrakt = true;
        SkipWatchedImportFromTrakt = false;
        PostWatchedHistory = true;
        PostUnwatchedHistory = true;
        PostSetWatched = true;
        PostSetUnwatched = true;
        ExtraLogging = false;
        ExportMediaInfo = false;
        SynchronizeCollections = true;
        Scrobble = true;
    }

    public string AccessToken { get; set; }

    public string RefreshToken { get; set; }

    public string LinkedMbUserId { get; set; }

    public bool UsesAdvancedRating { get; set; }

    public bool SkipUnwatchedImportFromTrakt { get; set; }

    public bool SkipWatchedImportFromTrakt { get; set; }

    public bool PostWatchedHistory { get; set; }

    public bool PostUnwatchedHistory { get; set; }

    public bool PostSetWatched { get; set; }

    public bool PostSetUnwatched { get; set; }

    public bool ExtraLogging { get; set; }

    public bool ExportMediaInfo { get; set; }

    public bool SynchronizeCollections { get; set; }

    public bool Scrobble { get; set; }

    public string[] LocationsExcluded { get; set; }

    public DateTime AccessTokenExpiration { get; set; }
}
