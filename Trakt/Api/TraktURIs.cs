namespace Trakt.Api;

public static class TraktUris
{
    public const string BaseUrl = "https://api.trakt.tv";
    public const string ClientId = "58f2251f1c9e7275e94fef723a8604e6848bbf86a0d97dda82382a6c3231608c";
    public const string ClientSecret = "bf9fce37cf45c1de91da009e7ac6fca905a35d7a718bf65a52f92199073a2503";

    public const string DeviceCode = BaseUrl + "/oauth/device/code";
    public const string DeviceToken = BaseUrl + "/oauth/device/token";
    public const string AccessToken = BaseUrl + "/oauth/token";

    public const string SyncCollectionAdd = BaseUrl + "/sync/collection";
    public const string SyncCollectionRemove = BaseUrl + "/sync/collection/remove";
    public const string SyncWatchedHistoryAdd = BaseUrl + "/sync/history";
    public const string SyncWatchedHistoryRemove = BaseUrl + "/sync/history/remove";
    public const string SyncRatingsAdd = BaseUrl + "/sync/ratings";

    public const string ScrobbleStart = BaseUrl + "/scrobble/start";
    public const string ScrobblePause = BaseUrl + "/scrobble/pause";
    public const string ScrobbleStop = BaseUrl + "/scrobble/stop";

    public const string WatchedMovies = BaseUrl + "/sync/watched/movies";
    public const string WatchedShows = BaseUrl + "/sync/watched/shows";
    public const string CollectedMovies = BaseUrl + "/sync/collection/movies?extended=metadata";
    public const string CollectedShows = BaseUrl + "/sync/collection/shows?extended=metadata";

    // Recommendations
    public const string RecommendationsMovies = BaseUrl + "/recommendations/movies";
    public const string RecommendationsShows = BaseUrl + "/recommendations/shows";

    // Recommendations
    public const string RecommendationsMoviesDismiss = BaseUrl + "/recommendations/movies/{0}";
    public const string RecommendationsShowsDismiss = BaseUrl + "/recommendations/shows/{0}";
}
