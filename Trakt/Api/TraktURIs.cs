namespace Trakt.Api
{
    public static class TraktUris
    {
        public const string Id = "229219b2db18f433b5503358d7055e366e7ffb5f1dc1d6904d131d0ed806ccac";
        public const string Secret = "3cf334f7afd23b32ca8a201cb66231425955ba7a77af1804a4f3053272798efd";

        #region POST URI's

        public const string Token = @"https://api.trakt.tv/oauth/token";

        public const string SyncCollectionAdd = @"https://api.trakt.tv/sync/collection";
        public const string SyncCollectionRemove = @"https://api.trakt.tv/sync/collection/remove";
        public const string SyncWatchedHistoryAdd = @"https://api.trakt.tv/sync/history";
        public const string SyncWatchedHistoryRemove = @"https://api.trakt.tv/sync/history/remove";
        public const string SyncRatingsAdd = @"https://api.trakt.tv/sync/ratings";

        public const string ScrobbleStart = @"https://api.trakt.tv/scrobble/start";
        public const string ScrobblePause = @"https://api.trakt.tv/scrobble/pause";
        public const string ScrobbleStop = @"https://api.trakt.tv/scrobble/stop";
        #endregion

        #region GET URI's

        public const string WatchedMovies = @"https://api.trakt.tv/sync/watched/movies";
        public const string WatchedShows = @"https://api.trakt.tv/sync/watched/shows";
        public const string CollectedMovies = @"https://api.trakt.tv/sync/collection/movies?extended=metadata";
        public const string CollectedShows = @"https://api.trakt.tv/sync/collection/shows?extended=metadata";

        // Recommendations
        public const string RecommendationsMovies = @"https://api.trakt.tv/recommendations/movies";
        public const string RecommendationsShows = @"https://api.trakt.tv/recommendations/shows";

        #endregion

        #region DELETE 

        // Recommendations
        public const string RecommendationsMoviesDismiss = @"https://api.trakt.tv/recommendations/movies/{0}";
        public const string RecommendationsShowsDismiss = @"https://api.trakt.tv/recommendations/shows/{0}";

        #endregion
    }
}

