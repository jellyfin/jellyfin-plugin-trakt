using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Trakt.Api;
using Trakt.Api.DataContracts.Users.Playback;
using Trakt.Api.DataContracts.Users.Watched;
using Trakt.Helpers;
using Episode = MediaBrowser.Controller.Entities.TV.Episode;

namespace Trakt.ScheduledTasks
{
    /// <summary>
    /// Task that will Sync each users trakt.tv profile with their local library. This task will only include
    /// watched states.
    /// </summary>
    public class SyncFromTraktTask : IScheduledTask
    {
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<SyncFromTraktTask> _logger;
        private readonly TraktApi _traktApi;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncFromTraktTask"/> class.
        /// </summary>
        /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
        /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
        /// <param name="userDataManager">Instance of the <see cref="IUserDataManager"/> interface.</param>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        public SyncFromTraktTask(
            ILoggerFactory loggerFactory,
            IUserManager userManager,
            IUserDataManager userDataManager,
            IHttpClientFactory httpClientFactory,
            IServerApplicationHost appHost,
            ILibraryManager libraryManager)
        {
            _userManager = userManager;
            _userDataManager = userDataManager;
            _libraryManager = libraryManager;
            _logger = loggerFactory.CreateLogger<SyncFromTraktTask>();
            _traktApi = new TraktApi(loggerFactory.CreateLogger<TraktApi>(), httpClientFactory, appHost, userDataManager);
        }

        /// <inheritdoc />
        public string Key => "TraktSyncFromTraktTask";

        /// <inheritdoc />
        public string Name => "Import watched states and playback progress from trakt.tv";

        /// <inheritdoc />
        public string Description => "Imports each user's watched/unwatched status and playback progress from trakt.tv to all items in the user's trakt.tv monitored locations";

        /// <inheritdoc />
        public string Category => "Trakt";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Enumerable.Empty<TaskTriggerInfo>();

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var users = _userManager.Users.Where(u => UserHelper.GetTraktUser(u) != null).ToList();

            // No point going further if we don't have users.
            if (users.Count == 0)
            {
                _logger.LogDebug("No Users returned");
                return;
            }

            // Purely for progress reporting
            var percentPerUser = 100 / users.Count;
            double currentProgress = 0;
            var numComplete = 0;

            foreach (var user in users)
            {
                try
                {
                    await SyncTraktDataForUser(user, currentProgress, progress, percentPerUser, cancellationToken).ConfigureAwait(false);

                    numComplete++;
                    currentProgress = percentPerUser * numComplete;
                    progress.Report(currentProgress);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing trakt.tv data for user {UserName}", user.Username);
                    throw;
                }
            }
        }

        private async Task SyncTraktDataForUser(Jellyfin.Data.Entities.User user, double currentProgress, IProgress<double> progress, double percentPerUser, CancellationToken cancellationToken)
        {
            var traktUser = UserHelper.GetTraktUser(user);

            if (traktUser.SkipUnwatchedImportFromTrakt
                && traktUser.SkipWatchedImportFromTrakt
                && traktUser.SkipPlaybackProgressImportFromTrakt)
            {
                _logger.LogDebug("User {Name} disabled (un)watched and playback syncing.", user.Username);
                return;
            }

            List<TraktMovieWatched> traktWatchedMovies;
            List<TraktShowWatched> traktWatchedShows;
            List<TraktMoviePaused> traktPausedMovies;
            List<TraktEpisodePaused> traktPausedShows;

            try
            {
                /*
                 * In order to be as accurate as possible. We need to download the user's show collection and the user's watched shows.
                 * It's unfortunate that trakt.tv doesn't explicitly supply a bulk method to determine shows that have not been watched
                 * like they do for movies.
                 */
                traktWatchedMovies = await _traktApi.SendGetAllWatchedMoviesRequest(traktUser).ConfigureAwait(false);
                traktWatchedShows = await _traktApi.SendGetWatchedShowsRequest(traktUser).ConfigureAwait(false);
                traktPausedMovies = await _traktApi.SendGetAllPausedMoviesRequest(traktUser).ConfigureAwait(false);
                traktPausedShows = await _traktApi.SendGetPausedShowsRequest(traktUser).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception handled");
                throw;
            }

            _logger.LogInformation("Trakt.tv watched movies for user {User}: {Count}", traktWatchedMovies.Count, user.Username);
            _logger.LogInformation("Trakt.tv paused movies for user {User}: {Count}", traktPausedMovies.Count, user.Username);
            _logger.LogInformation("Trakt.tv watched shows for user {User}: {Count}", traktWatchedShows.Count, user.Username);
            _logger.LogInformation("Trakt.tv paused shows for user {User}: {Count}", traktPausedShows.Count, user.Username);

            var baseQuery = new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[]
                {
                    BaseItemKind.Movie,
                    BaseItemKind.Episode
                },
                IsVirtualItem = false,
                OrderBy = new[]
                {
                    (ItemSortBy.SeriesSortName, SortOrder.Ascending),
                    (ItemSortBy.SortName, SortOrder.Ascending)
                }
            };

            var totalCount = _libraryManager.GetCount(baseQuery);

            const int Limit = 100;
            int offset = 0, previousCount;

            // Purely for progress reporting
            var percentPerIteration = percentPerUser / (totalCount / (double)Limit);

            do
            {
                baseQuery.Limit = Limit;
                baseQuery.StartIndex = offset;

                var mediaItems = _libraryManager.GetItemList(baseQuery);

                previousCount = mediaItems.Count;
                offset += Limit;

                mediaItems = mediaItems.Where(i => _traktApi.CanSync(i, traktUser)).ToList();

                if (mediaItems != null)
                {
                    // Purely for progress reporting
                    var percentPerItem = percentPerIteration / mediaItems.Count;

                    foreach (var movie in mediaItems.OfType<Movie>())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var matchedWatchedMovie = Extensions.FindMatch(movie, traktWatchedMovies);
                        var matchedPausedMovie = Extensions.FindMatch(movie, traktPausedMovies);
                        var userData = _userDataManager.GetUserData(user.Id, movie);
                        bool changed = false;

                        if (matchedWatchedMovie != null && !traktUser.SkipWatchedImportFromTrakt)
                        {
                            _logger.LogDebug("Movie is in watched list: \"{Name}\"", movie.Name);

                            DateTime? tLastPlayed = null;
                            if (DateTime.TryParse(matchedWatchedMovie.LastWatchedAt, out var value))
                            {
                                tLastPlayed = value;
                            }

                            // Set movie as watched
                            if (!userData.Played)
                            {
                                _logger.LogDebug("Marking movie \"{Name}\" as watched locally.", movie.Name);
                                userData.Played = true;
                                userData.LastPlayedDate = tLastPlayed ?? DateTime.Now;
                                changed = true;
                            }

                            // Keep the highest play count
                            if (userData.PlayCount < matchedWatchedMovie.Plays)
                            {
                                _logger.LogDebug("Adjusting movie play count locally: \"{Name}\"", movie.Name);
                                userData.PlayCount = matchedWatchedMovie.Plays;
                                changed = true;
                            }

                            // Update last played if remote time is more recent
                            if (tLastPlayed != null && userData.LastPlayedDate < tLastPlayed)
                            {
                                _logger.LogDebug("Adjusting movie last played date locally: \"{Name}\"", movie.Name);
                                userData.LastPlayedDate = tLastPlayed;
                                changed = true;
                            }
                        }
                        else if (!traktUser.SkipUnwatchedImportFromTrakt)
                        {
                            _logger.LogDebug("Movie is not in watched list: \"{Name}\"", movie.Name);

                            // Set movie as unwatched
                            if (userData.Played)
                            {
                                _logger.LogDebug("Marking movie as unwatched locally: \"{Name}\"", movie.Name);
                                userData.Played = false;
                                changed = true;
                            }
                        }

                        if (matchedPausedMovie != null && !traktUser.SkipPlaybackProgressImportFromTrakt)
                        {
                            _logger.LogDebug("Movie is in paused list: \"{Name}\"", movie.Name);
                            DateTime? paused = null;
                            if (DateTime.TryParse(matchedPausedMovie.PausedAt, out var value))
                            {
                                paused = value;
                            }

                            if (paused != null && userData.LastPlayedDate < paused)
                            {
                                var currentPlaybackTicks = userData.PlaybackPositionTicks;
                                var runtimeTicks = movie.GetRunTimeTicksForPlayState();
                                var traktPlaybackTicks = runtimeTicks != 0
                                    ? (long)matchedPausedMovie.Progress * runtimeTicks
                                    : 0;

                                if (traktPlaybackTicks > currentPlaybackTicks)
                                {
                                    _logger.LogDebug("Setting playback progress for movie locally: \"{Name}\"", movie.Name);
                                    userData.PlaybackPositionTicks = traktPlaybackTicks;
                                    changed = true;
                                }
                            }
                        }

                        // Only process if there's a change
                        if (changed)
                        {
                            _userDataManager.SaveUserData(
                                user.Id,
                                movie,
                                userData,
                                UserDataSaveReason.Import,
                                cancellationToken);
                        }

                        // Purely for progress reporting
                        currentProgress += percentPerItem;
                        progress.Report(currentProgress);
                    }

                    foreach (var episode in mediaItems.OfType<Episode>())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var matchedWatchedShow = Extensions.FindMatch(episode.Series, traktWatchedShows);
                        var matchedPausedEpisode = Extensions.FindMatch(episode, traktPausedShows);
                        var userData = _userDataManager.GetUserData(user.Id, episode);
                        bool changed = false;

                        if (matchedWatchedShow != null)
                        {
                            var matchedWatchedSeason = matchedWatchedShow.Seasons.FirstOrDefault(
                                    tSeason => tSeason.Number == (episode.ParentIndexNumber == 0
                                            ? 0
                                            : episode.ParentIndexNumber ?? 1));

                            // Keep track of the shows rewatch cycles
                            DateTime? tLastReset = null;
                            if (DateTime.TryParse(matchedWatchedShow.ResetAt, out var resetValue))
                            {
                                tLastReset = resetValue;
                            }

                            // If it's not a match then it means trakt.tv doesn't know about the season, leave the watched state alone and move on
                            if (matchedWatchedSeason != null)
                            {
                                var matchedWatchedEpisode = matchedWatchedSeason.Episodes.FirstOrDefault(x => x.Number == (episode.IndexNumber ?? -1));

                                // Prepend a check if the matched episode is on a rewatch cycle and
                                // discard it if the last play date was before the reset date
                                if (matchedWatchedEpisode != null
                                    && tLastReset != null
                                    && DateTime.TryParse(matchedWatchedEpisode.LastWatchedAt, out var lastPlayedValue)
                                    && lastPlayedValue < tLastReset)
                                {
                                    matchedWatchedEpisode = null;
                                }

                                if (matchedWatchedEpisode != null && !traktUser.SkipWatchedImportFromTrakt)
                                {
                                    _logger.LogDebug("Episode is in watched list: \"{Data}\"", GetVerboseEpisodeData(episode));

                                    DateTime? tLastPlayed = null;
                                    if (DateTime.TryParse(matchedWatchedEpisode.LastWatchedAt, out var lastWatchedValue))
                                    {
                                        tLastPlayed = lastWatchedValue;
                                    }

                                    // Set episode as watched
                                    if (!userData.Played)
                                    {
                                        _logger.LogDebug("Marking episode as unwatched locally: \"{Data}\"", GetVerboseEpisodeData(episode));
                                        userData.Played = true;
                                        userData.LastPlayedDate = tLastPlayed ?? DateTime.Now;
                                        changed = true;
                                    }

                                    // Keep the highest play count
                                    if (userData.PlayCount < matchedWatchedEpisode.Plays)
                                    {
                                        _logger.LogDebug("Adjusting episode playcount locally: \"{Data}\"", GetVerboseEpisodeData(episode));
                                        userData.PlayCount = matchedWatchedEpisode.Plays;
                                        changed = true;
                                    }

                                    // Update last played if remote time is more recent
                                    if (tLastPlayed != null && userData.LastPlayedDate < tLastPlayed)
                                    {
                                        _logger.LogDebug("Setting episode last played date locally: \"{Data}\"", GetVerboseEpisodeData(episode));
                                        userData.LastPlayedDate = tLastPlayed;
                                        changed = true;
                                    }
                                }
                                else if (!traktUser.SkipUnwatchedImportFromTrakt)
                                {
                                    _logger.LogDebug("Episode not in watched list: \"{Data}\"", GetVerboseEpisodeData(episode));
                                    if (userData.Played)
                                    {
                                        _logger.LogDebug("Marking episode as unwatched locally: \"{Data}\"", GetVerboseEpisodeData(episode));
                                        userData.Played = false;
                                        changed = true;
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogDebug("No season data found for \"{Episode}\"", GetVerboseEpisodeData(episode));
                            }
                        }
                        else
                        {
                            _logger.LogDebug("No show data found for \"{Episode}\"", GetVerboseEpisodeData(episode));
                        }

                        if (matchedPausedEpisode != null && !traktUser.SkipPlaybackProgressImportFromTrakt)
                        {
                            _logger.LogDebug("Episode is in paused list: {Data}", GetVerboseEpisodeData(episode));

                            DateTime? paused = null;
                            if (DateTime.TryParse(matchedPausedEpisode.PausedAt, out var value))
                            {
                                paused = value;
                            }

                            if (paused != null && userData.LastPlayedDate < paused)
                            {
                                var currentPlaybackTicks = userData.PlaybackPositionTicks;
                                var runtimeTicks = episode.GetRunTimeTicksForPlayState();
                                var traktPlaybackTicks = runtimeTicks != 0
                                    ? (long)matchedPausedEpisode.Progress * runtimeTicks
                                    : 0;

                                if (traktPlaybackTicks > currentPlaybackTicks)
                                {
                                    _logger.LogDebug("Setting playback progress for episode locally: \"{Data}\"", GetVerboseEpisodeData(episode));
                                    userData.PlaybackPositionTicks = traktPlaybackTicks;
                                    changed = true;
                                }
                            }
                        }

                        // Only process if changed
                        if (changed)
                        {
                            _userDataManager.SaveUserData(
                                user.Id,
                                episode,
                                userData,
                                UserDataSaveReason.Import,
                                cancellationToken);
                        }

                        // Purely for progress reporting
                        currentProgress += percentPerItem;
                        progress.Report(currentProgress);
                    }
                }
            }
            while (previousCount != 0);
        }

        private static string GetVerboseEpisodeData(Episode episode)
        {
            var episodeString = new StringBuilder()
                .Append("Episode: ")
                .Append(episode.ParentIndexNumber != null ? episode.ParentIndexNumber.ToString() : "null")
                .Append('x')
                .Append(episode.IndexNumber != null ? episode.IndexNumber.ToString() : "null")
                .Append(" '").Append(episode.Name).Append("' ")
                .Append("Series: '")
                .Append(episode.Series != null
                    ? !string.IsNullOrWhiteSpace(episode.Series.Name)
                        ? episode.Series.Name
                        : "null property"
                    : "null class")
                .Append('\'');

            return episodeString.ToString();
        }
    }
}
