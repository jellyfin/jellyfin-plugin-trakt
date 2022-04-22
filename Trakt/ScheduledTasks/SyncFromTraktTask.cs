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
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Trakt.Api;
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
        /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        public SyncFromTraktTask(
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
            _logger = loggerFactory.CreateLogger<SyncFromTraktTask>();
            _traktApi = new TraktApi(loggerFactory.CreateLogger<TraktApi>(), httpClientFactory, appHost, userDataManager, fileSystem);
        }

        /// <inheritdoc />
        public string Key => "TraktSyncFromTraktTask";

        /// <inheritdoc />
        public string Name => "Import playstates from Trakt.tv";

        /// <inheritdoc />
        public string Description => "Sync Watched/Unwatched status from Trakt.tv for each Jellyfin user that has a configured trakt.tv account";

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

            List<TraktMovieWatched> traktWatchedMovies;
            List<TraktShowWatched> traktWatchedShows;

            try
            {
                /*
                 * In order to be as accurate as possible. We need to download the user's show collection and the user's watched shows.
                 * It's unfortunate that trakt.tv doesn't explicitly supply a bulk method to determine shows that have not been watched
                 * like they do for movies.
                 */
                traktWatchedMovies = await _traktApi.SendGetAllWatchedMoviesRequest(traktUser).ConfigureAwait(false);
                traktWatchedShows = await _traktApi.SendGetWatchedShowsRequest(traktUser).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception handled");
                throw;
            }

            _logger.LogDebug("Trakt.tv watched movies count = {Count}", traktWatchedMovies.Count);
            _logger.LogDebug("Trakt.tv watched shows count = {Count}", traktWatchedShows.Count);

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
            // Purely for progress reporting
            var percentPerItem = percentPerUser / totalCount;

            const int Limit = 100;
            int offset = 0, previousCount;

            do
            {
                baseQuery.Limit = Limit;
                baseQuery.StartIndex = offset;

                var mediaItems = _libraryManager.GetItemList(baseQuery)
                    .Where(item => !traktUser.LocationsExcluded.Any(directory => item.Path.Contains(directory, StringComparison.OrdinalIgnoreCase)))
                    .ToHashSet();
                previousCount = mediaItems.Count;
                offset += Limit;

                if (mediaItems.Any())
                {
                    mediaItems = mediaItems.Where(i => _traktApi.CanSync(i, traktUser)).ToHashSet();

                    foreach (var movie in mediaItems.OfType<Movie>())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var matchedMovie = Extensions.FindMatch(movie, traktWatchedMovies);

                        if (matchedMovie != null)
                        {
                            _logger.LogDebug("Movie is in watched list: \"{Name}\"", movie.Name);

                            var userData = _userDataManager.GetUserData(user.Id, movie);
                            bool changed = false;

                            DateTime? tLastPlayed = null;
                            if (DateTime.TryParse(matchedMovie.LastWatchedAt, out var value))
                            {
                                tLastPlayed = value;
                            }

                            // Set movie as watched
                            if (!userData.Played)
                            {
                                userData.Played = true;
                                userData.LastPlayedDate = tLastPlayed ?? DateTime.Now;
                                changed = true;
                            }

                            // Keep the highest play count
                            if (userData.PlayCount < matchedMovie.Plays)
                            {
                                userData.PlayCount = matchedMovie.Plays;
                                changed = true;
                            }

                            // Update last played if remote time is more recent
                            if (tLastPlayed != null && userData.LastPlayedDate < tLastPlayed)
                            {
                                userData.LastPlayedDate = tLastPlayed;
                                changed = true;
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
                        }
                        else
                        {
                            _logger.LogDebug("No watched data found for \"{Movie}\"", movie.Name);
                        }

                        // Purely for progress reporting
                        currentProgress += percentPerItem;
                        progress.Report(currentProgress);
                    }

                    foreach (var episode in mediaItems.OfType<Episode>())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var matchedShow = Extensions.FindMatch(episode.Series, traktWatchedShows);

                        if (matchedShow != null)
                        {
                            var matchedSeason = matchedShow.Seasons.FirstOrDefault(
                                    tSeason => tSeason.Number == (episode.ParentIndexNumber == 0
                                            ? 0
                                            : episode.ParentIndexNumber ?? 1));

                            // Keep track of the shows rewatch cycles
                            DateTime? tLastReset = null;
                            if (DateTime.TryParse(matchedShow.ResetAt, out var resetValue))
                            {
                                tLastReset = resetValue;
                            }

                            // If it's not a match then it means trakt.tv doesn't know about the season, leave the watched state alone and move on
                            if (matchedSeason != null)
                            {
                                // Episode is in user's libary. Now we need to determine if it's watched
                                var userData = _userDataManager.GetUserData(user.Id, episode);
                                bool changed = false;

                                var matchedEpisode = matchedSeason.Episodes.FirstOrDefault(x => x.Number == (episode.IndexNumber ?? -1));

                                // Prepend a check if the matched episode is on a rewatch cycle and
                                // discard it if the last play date was before the reset date
                                if (matchedEpisode != null
                                    && tLastReset != null
                                    && DateTime.TryParse(matchedEpisode.LastWatchedAt, out var lastPlayedValue)
                                    && lastPlayedValue < tLastReset)
                                {
                                    matchedEpisode = null;
                                }

                                if (matchedEpisode != null)
                                {
                                    _logger.LogDebug("Episode is in watched list: \"{Data}\"", GetVerboseEpisodeData(episode));

                                    if (!traktUser.SkipWatchedImportFromTrakt)
                                    {
                                        DateTime? tLastPlayed = null;
                                        if (DateTime.TryParse(matchedEpisode.LastWatchedAt, out var lastWatchedValue))
                                        {
                                            tLastPlayed = lastWatchedValue;
                                        }

                                        // Set episode as watched
                                        if (!userData.Played)
                                        {
                                            userData.Played = true;
                                            userData.LastPlayedDate = tLastPlayed ?? DateTime.Now;
                                            changed = true;
                                        }

                                        // Keep the highest play count
                                        if (userData.PlayCount < matchedEpisode.Plays)
                                        {
                                            userData.PlayCount = matchedEpisode.Plays;
                                            changed = true;
                                        }

                                        // Update last played if remote time is more recent
                                        if (tLastPlayed != null && userData.LastPlayedDate < tLastPlayed)
                                        {
                                            userData.LastPlayedDate = tLastPlayed;
                                            changed = true;
                                        }
                                    }
                                }
                                else if (!traktUser.SkipUnwatchedImportFromTrakt)
                                {
                                    userData.Played = false;
                                    userData.PlayCount = 0;
                                    userData.LastPlayedDate = null;
                                    changed = true;
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
                            }
                            else
                            {
                                _logger.LogDebug("No season watched data found for \"{Episode}\"", GetVerboseEpisodeData(episode));
                            }
                        }
                        else
                        {
                            _logger.LogDebug("No show watched data found for \"{Episode}\"", GetVerboseEpisodeData(episode));
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
