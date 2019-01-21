namespace Trakt.Api
{
    public static class TraktUris
    {
        public const string BaseUrl = "https://api.trakt.tv";
        public const string ClientId = "redacted";
        public const string ClientSecret = "redacted";

        #region POST URI's

        public static readonly string DeviceCode = $@"{BaseUrl}/oauth/device/code";
        public static readonly string DeviceToken = $@"{BaseUrl}/oauth/device/token";

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

