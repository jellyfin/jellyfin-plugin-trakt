namespace Trakt.Api;

/// <summary>
/// The trakt.tv URI class.
/// </summary>
public static class TraktUris
{
    /// <summary>
    /// The base URL.
    /// </summary>
    public const string BaseUrl = "https://api.trakt.tv";

    /// <summary>
    /// The client id.
    /// </summary>
    public const string ClientId = "bfdd2e032c30c35b368f97ef4ec81587b899bcb028b91a1d4ba5589a4b6a7267";

    /// <summary>
    /// The client secret.
    /// </summary>
    public const string ClientSecret = "bf9fce37cf45c1de91da009e7ac6fca905a35d7a718bf65a52f92199073a2503";

    /// <summary>
    /// The device code URI.
    /// </summary>
    public const string DeviceCode = BaseUrl + "/oauth/device/code";

    /// <summary>
    /// The device token URI.
    /// </summary>
    public const string DeviceToken = BaseUrl + "/oauth/device/token";

    /// <summary>
    /// The access token URI.
    /// </summary>
    public const string AccessToken = BaseUrl + "/oauth/token";

    /// <summary>
    /// The token revoke URI.
    /// </summary>
    public const string RevokeToken = BaseUrl + "/oauth/revoke";

    /// <summary>
    /// The collection sync add URI.
    /// </summary>
    public const string SyncCollectionAdd = BaseUrl + "/sync/collection";

    /// <summary>
    /// The collection sync remove URI.
    /// </summary>
    public const string SyncCollectionRemove = BaseUrl + "/sync/collection/remove";

    /// <summary>
    /// The watched episodes history URI.
    /// </summary>
    public const string SyncWatchedEpisodesHistory = BaseUrl + "/sync/history/episodes?page={page}&limit={limit}";

    /// <summary>
    /// The watched history add URI.
    /// </summary>
    public const string SyncWatchedHistoryAdd = BaseUrl + "/sync/history";

    /// <summary>
    /// The watched history remove URI.
    /// </summary>
    public const string SyncWatchedHistoryRemove = BaseUrl + "/sync/history/remove";

    /// <summary>
    /// The ratings add URI.
    /// </summary>
    public const string SyncRatingsAdd = BaseUrl + "/sync/ratings";

    /// <summary>
    /// The scrobble start URI.
    /// </summary>
    public const string ScrobbleStart = BaseUrl + "/scrobble/start";

    /// <summary>
    /// The scrobble pause URI.
    /// </summary>
    public const string ScrobblePause = BaseUrl + "/scrobble/pause";

    /// <summary>
    /// The scrobble stop URI.
    /// </summary>
    public const string ScrobbleStop = BaseUrl + "/scrobble/stop";

    /// <summary>
    /// The watched movies URI.
    /// </summary>
    public const string WatchedMovies = BaseUrl + "/sync/watched/movies?page={page}&limit={limit}";

    /// <summary>
    /// The watched shows URI.
    /// </summary>
    public const string WatchedShows = BaseUrl + "/sync/watched/shows?page={page}&limit={limit}";

    /// <summary>
    /// The paused movies URI.
    /// </summary>
    public const string PausedMovies = BaseUrl + "/sync/playback/movies?page={page}&limit={limit}";

    /// <summary>
    /// The paused shows URI.
    /// </summary>
    public const string PausedEpisodes = BaseUrl + "/sync/playback/episodes?page={page}&limit={limit}";

    /// <summary>
    /// The collected movies URI.
    /// </summary>
    public const string CollectedMovies = BaseUrl + "/sync/collection/movies?extended=metadata&page={page}&limit={limit}";

    /// <summary>
    /// The collected series URI.
    /// </summary>
    public const string CollectedShows = BaseUrl + "/sync/collection/shows?extended=metadata&page={page}&limit={limit}";

    /// <summary>
    /// The movies recommendations URI.
    /// </summary>
    public const string RecommendationsMovies = BaseUrl + "/recommendations/movies";

    /// <summary>
    /// The shows recommendations URI.
    /// </summary>
    public const string RecommendationsShows = BaseUrl + "/recommendations/shows";
}
