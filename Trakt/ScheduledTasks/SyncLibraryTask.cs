using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Trakt.Api;
using Trakt.Api.DataContracts.Sync;
using Trakt.Helpers;
using Trakt.Model;
using Trakt.Model.Enums;

namespace Trakt.ScheduledTasks
{
    /// <summary>
    /// Task that will Sync each users local library with their respective trakt.tv profiles. This task will only include
    /// titles, watched states will be synced in other tasks.
    /// </summary>
    public class SyncLibraryTask : IScheduledTask
    {
        private readonly IUserManager _userManager;

        private readonly ILogger<SyncLibraryTask> _logger;

        private readonly TraktApi _traktApi;

        private readonly IUserDataManager _userDataManager;

        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncLibraryTask"/> class.
        /// </summary>
        /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
        /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
        /// <param name="userDataManager">Instance of the <see cref="IUserDataManager"/> interface.</param>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
        /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        public SyncLibraryTask(
            ILoggerFactory loggerFactory,
            IUserManager userManager,
            IUserDataManager userDataManager,
            IHttpClientFactory httpClientFactory,
            IServerApplicationHost appHost,
            IFileSystem fileSystem,
            ILibraryManager libraryManager)
        {
            _userManager = userManager;
            _userDataManager = userDataManager;
            _libraryManager = libraryManager;
            _logger = loggerFactory.CreateLogger<SyncLibraryTask>();
            _traktApi = new TraktApi(loggerFactory.CreateLogger<TraktApi>(), httpClientFactory, appHost, userDataManager, fileSystem);
        }

        /// <inheritdoc />
        public string Key => "TraktSyncLibraryTask";

        /// <inheritdoc />
        public string Name => "Sync library to trakt.tv";

        /// <inheritdoc />
        public string Category => "Trakt";

        /// <inheritdoc />
        public string Description
            => "Adds any media that is in each users trakt.tv monitored locations to their trakt.tv profile";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Enumerable.Empty<TaskTriggerInfo>();

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var users = _userManager.Users.Where(u => UserHelper.GetTraktUser(u) != null).ToList();

            // No point going further if we don't have users.
            if (users.Count == 0)
            {
                _logger.LogInformation("No Users returned");
                return;
            }

            foreach (var user in users)
            {
                var traktUser = UserHelper.GetTraktUser(user);

                // I'll leave this in here for now, but in reality this continue should never be reached.
                if (string.IsNullOrEmpty(traktUser?.LinkedMbUserId))
                {
                    _logger.LogError("traktUser is either null or has no linked MB account");
                    continue;
                }

                await
                    SyncUserLibrary(user, traktUser, progress.Split(users.Count), cancellationToken)
                        .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Count media items and call <see cref="SyncMovies"/> and <see cref="SyncShows"/>.
        /// </summary>
        /// <param name="user">The <see cref="Jellyfin.Data.Entities.User"/>.</param>
        /// <param name="traktUser">The <see cref="TraktUser"/>.</param>
        /// <param name="progress">The progress.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>Task.</returns>
        private async Task SyncUserLibrary(
            Jellyfin.Data.Entities.User user,
            TraktUser traktUser,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            await SyncMovies(user, traktUser, progress.Split(2), cancellationToken).ConfigureAwait(false);
            await SyncShows(user, traktUser, progress.Split(2), cancellationToken).ConfigureAwait(false);
        }

        private async Task SyncMovies(
            Jellyfin.Data.Entities.User user,
            TraktUser traktUser,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            /*
             * In order to sync watched status to trakt.tv we need to know what's been watched on trakt.tv already. This
             * will stop us from endlessly incrementing the watched values on the site.
             */
            var traktWatchedMovies = await _traktApi.SendGetAllWatchedMoviesRequest(traktUser).ConfigureAwait(false);
            var traktCollectedMovies = await _traktApi.SendGetAllCollectedMoviesRequest(traktUser).ConfigureAwait(false);

            var baseQuery = new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                IsVirtualItem = false,
                OrderBy = new[] { (ItemSortBy.SortName, SortOrder.Ascending) }
            };

            var totalCount = _libraryManager.GetCount(baseQuery);
            var decisionProgress = progress.Split(4).Split(totalCount);

            var collectedMovies = new List<Movie>();
            var playedMovies = new List<Movie>();
            var unplayedMovies = new List<Movie>();

            const int Limit = 100;
            int offset = 0, previousCount;

            do
            {
                baseQuery.Limit = Limit;
                baseQuery.StartIndex = offset;

                var libraryMovies = _libraryManager.GetItemList(baseQuery);
                previousCount = libraryMovies.Count;
                offset += Limit;

                foreach (var libraryMovie in libraryMovies.OfType<Movie>().Where(x => _traktApi.CanSync(x, traktUser)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var userData = _userDataManager.GetUserData(user.Id, libraryMovie);

                    if (traktUser.SynchronizeCollections)
                    {
                        // If movie is not collected, or (export media info setting is enabled and every collected matching movie has different metadata), collect it
                        var collectedMatchingMovies = SyncFromTraktTask.FindMatches(libraryMovie, traktCollectedMovies).ToList();
                        if (collectedMatchingMovies.Count == 0
                            || (traktUser.ExportMediaInfo && collectedMatchingMovies.All(collectedMovie => collectedMovie.MetadataIsDifferent(libraryMovie))))
                        {
                            collectedMovies.Add(libraryMovie);
                        }
                    }

                    var movieWatched = SyncFromTraktTask.FindMatch(libraryMovie, traktWatchedMovies);

                    // If the movie has been played locally and is unplayed on trakt.tv then add it to the list
                    if (userData.Played)
                    {
                        if (movieWatched == null)
                        {
                            if (traktUser.PostWatchedHistory)
                            {
                                playedMovies.Add(libraryMovie);
                            }
                            else if (!traktUser.SkipUnwatchedImportFromTrakt)
                            {
                                if (userData.Played)
                                {
                                    userData.Played = false;

                                    _userDataManager.SaveUserData(
                                        user.Id,
                                        libraryMovie,
                                        userData,
                                        UserDataSaveReason.Import,
                                        cancellationToken);
                                }
                            }
                        }
                    }
                    else
                    {
                        // If the show has not been played locally but is played on trakt.tv then add it to the unplayed list
                        if (movieWatched != null && traktUser.PostUnwatchedHistory)
                        {
                            unplayedMovies.Add(libraryMovie);
                        }
                    }

                    decisionProgress.Report(100);
                }
            }
            while (previousCount != 0);

            // Send movies to mark collected
            if (traktUser.SynchronizeCollections)
            {
                await SendMovieCollectionUpdates(true, traktUser, collectedMovies, progress.Split(4), cancellationToken).ConfigureAwait(false);
            }

            // Send movies to mark watched
            await SendMoviePlaystateUpdates(true, traktUser, playedMovies, progress.Split(4), cancellationToken).ConfigureAwait(false);

            // Send movies to mark unwatched
            await SendMoviePlaystateUpdates(false, traktUser, unplayedMovies, progress.Split(4), cancellationToken).ConfigureAwait(false);
        }

        private async Task SendMovieCollectionUpdates(
            bool collected,
            TraktUser traktUser,
            ICollection<Movie> movies,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Movies to {State} Collection {Count}", collected ? "add to" : "remove from", movies.Count);
            if (movies.Count > 0)
            {
                try
                {
                    var dataContracts =
                        await _traktApi.SendLibraryUpdateAsync(
                                movies.ToList().AsReadOnly(),
                                traktUser,
                                collected ? EventType.Add : EventType.Remove,
                                cancellationToken)
                            .ConfigureAwait(false);
                    if (dataContracts != null)
                    {
                        foreach (var traktSyncResponse in dataContracts)
                        {
                            LogTraktResponseDataContract(traktSyncResponse);
                        }
                    }
                }
                catch (ArgumentNullException argNullEx)
                {
                    _logger.LogError(argNullEx, "ArgumentNullException handled sending movies to trakt.tv");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception handled sending movies to trakt.tv");
                }

                progress.Report(100);
            }
        }

        private async Task SendMoviePlaystateUpdates(
            bool seen,
            TraktUser traktUser,
            ICollection<Movie> playedMovies,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Movies to set {State}watched: {Count}", seen ? string.Empty : "un", playedMovies.Count);
            if (playedMovies.Count > 0)
            {
                try
                {
                    var dataContracts =
                        await _traktApi.SendMoviePlaystateUpdates(
                            playedMovies.ToList().AsReadOnly(),
                            traktUser,
                            seen,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (dataContracts != null)
                    {
                        foreach (var traktSyncResponse in dataContracts)
                        {
                            LogTraktResponseDataContract(traktSyncResponse);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error updating movie play states");
                }

                progress.Report(100);
            }
        }

        private async Task SyncShows(
            Jellyfin.Data.Entities.User user,
            TraktUser traktUser,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var traktWatchedShows = await _traktApi.SendGetWatchedShowsRequest(traktUser).ConfigureAwait(false);
            var traktCollectedShows = await _traktApi.SendGetCollectedShowsRequest(traktUser).ConfigureAwait(false);

            var baseQuery = new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.Episode },
                IsVirtualItem = false,
                OrderBy = new[] { (ItemSortBy.SeriesSortName, SortOrder.Ascending) }
            };

            var totalCount = _libraryManager.GetCount(baseQuery);
            var decisionProgress = progress.Split(4).Split(totalCount);

            var collectedEpisodes = new List<Episode>();
            var playedEpisodes = new List<Episode>();
            var unplayedEpisodes = new List<Episode>();

            const int Limit = 100;
            int offset = 0, previousCount;

            do
            {
                baseQuery.Limit = Limit;
                baseQuery.StartIndex = offset;

                var episodeItems = _libraryManager.GetItemList(baseQuery);
                previousCount = episodeItems.Count;
                offset += Limit;

                foreach (var episode in episodeItems.OfType<Episode>().Where(x => _traktApi.CanSync(x, traktUser)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var userData = _userDataManager.GetUserData(user.Id, episode);
                    var isPlayedTraktTv = false;
                    var traktWatchedShow = SyncFromTraktTask.FindMatch(episode.Series, traktWatchedShows);

                    if (traktWatchedShow?.Seasons != null && traktWatchedShow.Seasons.Count > 0)
                    {
                        isPlayedTraktTv =
                            traktWatchedShow.Seasons.Any(
                                season =>
                                    season.Number == episode.GetSeasonNumber() && season.Episodes != null
                                                                               && season.Episodes.Any(te => te.Number == episode.IndexNumber && te.Plays > 0));
                    }

                    // if the show has been played locally and is unplayed on trakt.tv then add it to the list
                    if (userData != null && userData.Played && !isPlayedTraktTv)
                    {
                        if (traktUser.PostWatchedHistory)
                        {
                            playedEpisodes.Add(episode);
                        }
                        else if (!traktUser.SkipUnwatchedImportFromTrakt)
                        {
                            if (userData.Played)
                            {
                                userData.Played = false;

                                _userDataManager.SaveUserData(
                                    user.Id,
                                    episode,
                                    userData,
                                    UserDataSaveReason.Import,
                                    cancellationToken);
                            }
                        }
                    }
                    else if (userData != null && !userData.Played && isPlayedTraktTv && traktUser.PostUnwatchedHistory)
                    {
                        // If the show has not been played locally but is played on trakt.tv then add it to the unplayed list
                        unplayedEpisodes.Add(episode);
                    }

                    if (traktUser.SynchronizeCollections)
                    {
                        var traktCollectedShow = SyncFromTraktTask.FindMatch(episode.Series, traktCollectedShows);
                        if (traktCollectedShow?.Seasons == null
                            || traktCollectedShow.Seasons.All(x => x.Number != episode.ParentIndexNumber)
                            || traktCollectedShow.Seasons.First(x => x.Number == episode.ParentIndexNumber)
                                .Episodes.All(e => e.Number != episode.IndexNumber))
                        {
                            collectedEpisodes.Add(episode);
                        }
                    }

                    decisionProgress.Report(100);
                }
            }
            while (previousCount != 0);

            if (traktUser.SynchronizeCollections)
            {
                await SendEpisodeCollectionUpdates(true, traktUser, collectedEpisodes, progress.Split(4), cancellationToken).ConfigureAwait(false);
            }

            await SendEpisodePlaystateUpdates(true, traktUser, playedEpisodes, progress.Split(4), cancellationToken).ConfigureAwait(false);

            await SendEpisodePlaystateUpdates(false, traktUser, unplayedEpisodes, progress.Split(4), cancellationToken).ConfigureAwait(false);
        }

        private async Task SendEpisodePlaystateUpdates(
            bool seen,
            TraktUser traktUser,
            ICollection<Episode> playedEpisodes,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Episodes to set {State}watched: {Count}", seen ? string.Empty : "un", playedEpisodes.Count);
            if (playedEpisodes.Count > 0)
            {
                try
                {
                    var dataContracts =
                        await _traktApi.SendEpisodePlaystateUpdates(
                            playedEpisodes.ToList().AsReadOnly(),
                            traktUser,
                            seen,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (dataContracts != null)
                    {
                        foreach (var con in dataContracts)
                        {
                            LogTraktResponseDataContract(con);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error updating episode play states");
                }

                progress.Report(100);
            }
        }

        private async Task SendEpisodeCollectionUpdates(
            bool collected,
            TraktUser traktUser,
            ICollection<Episode> collectedEpisodes,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Episodes to add to Collection: {Count}", collectedEpisodes.Count);
            if (collectedEpisodes.Count > 0)
            {
                try
                {
                    var dataContracts =
                        await _traktApi.SendLibraryUpdateAsync(
                                collectedEpisodes,
                                traktUser,
                                collected ? EventType.Add : EventType.Remove,
                                cancellationToken)
                            .ConfigureAwait(false);
                    if (dataContracts != null)
                    {
                        foreach (var traktSyncResponse in dataContracts)
                        {
                            LogTraktResponseDataContract(traktSyncResponse);
                        }
                    }
                }
                catch (ArgumentNullException argNullEx)
                {
                    _logger.LogError(argNullEx, "ArgumentNullException handled sending episodes to trakt.tv");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception handled sending episodes to trakt.tv");
                }

                progress.Report(100);
            }
        }

        private void LogTraktResponseDataContract(TraktSyncResponse dataContract)
        {
            _logger.LogDebug("TraktResponse Added Movies: {Count}", dataContract.Added.Movies);
            _logger.LogDebug("TraktResponse Added Shows: {Count}", dataContract.Added.Shows);
            _logger.LogDebug("TraktResponse Added Seasons: {Count}", dataContract.Added.Seasons);
            _logger.LogDebug("TraktResponse Added Episodes: {Count}", dataContract.Added.Episodes);
            foreach (var traktMovie in dataContract.NotFound.Movies)
            {
                _logger.LogError("TraktResponse not Found: {@TraktMovie}", traktMovie);
            }

            foreach (var traktShow in dataContract.NotFound.Shows)
            {
                _logger.LogError("TraktResponse not Found: {@TraktShow}", traktShow);
            }

            foreach (var traktSeason in dataContract.NotFound.Seasons)
            {
                _logger.LogError("TraktResponse not Found: {@TraktSeason}", traktSeason);
            }

            foreach (var traktEpisode in dataContract.NotFound.Episodes)
            {
                _logger.LogError("TraktResponse not Found: {@TraktEpisode}", traktEpisode);
            }
        }
    }
}
