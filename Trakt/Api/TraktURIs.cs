namespace Trakt.Api
{
    public static class TraktUris
    {
        public const string BaseUrl = "https://api.trakt.tv";
        public const string ClientId = "58f2251f1c9e7275e94fef723a8604e6848bbf86a0d97dda82382a6c3231608c";
        public const string ClientSecret = "bf9fce37cf45c1de91da009e7ac6fca905a35d7a718bf65a52f92199073a2503";

        #region POST URI's

        public static readonly string DeviceCode = $@"{BaseUrl}/oauth/device/code";
        public static readonly string DeviceToken = $@"{BaseUrl}/oauth/device/token";
        public static readonly string AccessToken = $@"{BaseUrl}/oauth/token";

        public static readonly string SyncCollectionAdd = $@"{BaseUrl}/sync/collection";
        public static readonly string SyncCollectionRemove = $@"{BaseUrl}/sync/collection/remove";
        public static readonly string SyncWatchedHistoryAdd = $@"{BaseUrl}/sync/history";
        public static readonly string SyncWatchedHistoryRemove = $@"{BaseUrl}/sync/history/remove";
        public static readonly string SyncRatingsAdd = $@"{BaseUrl}/sync/ratings";

        public static readonly string ScrobbleStart = $@"{BaseUrl}/scrobble/start";
        public static readonly string ScrobblePause = $@"{BaseUrl}/scrobble/pause";
        public static readonly string ScrobbleStop = $@"{BaseUrl}/scrobble/stop";

        #endregion

        #region GET URI's

        public static readonly string WatchedMovies = $@"{BaseUrl}/sync/watched/movies";
        public static readonly string WatchedShows = $@"{BaseUrl}/sync/watched/shows";
        public static readonly string CollectedMovies = $@"{BaseUrl}/sync/collection/movies?extended=metadata";
        public static readonly string CollectedShows = $@"{BaseUrl}/sync/collection/shows?extended=metadata";

        // Recommendations
        public static readonly string RecommendationsMovies = $@"{BaseUrl}/recommendations/movies";
        public static readonly string RecommendationsShows = $@"{BaseUrl}/recommendations/shows";

        #endregion

        #region DELETE 

        // Recommendations
        public static readonly string RecommendationsMoviesDismiss = $@"{BaseUrl}/recommendations/movies/{0}";
        public static readonly string RecommendationsShowsDismiss = $@"{BaseUrl}/recommendations/shows/{0}";

        #endregion
    }
}

